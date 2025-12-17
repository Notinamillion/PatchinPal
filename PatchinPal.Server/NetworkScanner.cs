using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PatchinPal.Common;

namespace PatchinPal.Server
{
    /// <summary>
    /// Scans the network to discover Windows machines
    /// </summary>
    public class NetworkScanner
    {
        private readonly MachineRepository _repository;
        private readonly int _defaultClientPort;
        private readonly int _scanTimeout;
        private readonly int _maxThreads;

        public NetworkScanner(MachineRepository repository)
        {
            _repository = repository;
            _defaultClientPort = int.Parse(ConfigurationManager.AppSettings["DefaultClientPort"] ?? "8090");
            _scanTimeout = int.Parse(ConfigurationManager.AppSettings["ScanTimeout"] ?? "1000");
            _maxThreads = int.Parse(ConfigurationManager.AppSettings["ScanThreads"] ?? "50");
        }

        /// <summary>
        /// Scan a subnet for Windows machines
        /// </summary>
        /// <param name="subnet">Subnet in CIDR notation (e.g., "192.168.1.0/24")</param>
        public List<MachineInfo> ScanSubnet(string subnet)
        {
            List<string> ipAddresses = GenerateIpRange(subnet);
            Console.WriteLine($"Scanning {ipAddresses.Count} IP addresses with {_maxThreads} threads...\n");

            var discoveredMachines = new List<MachineInfo>();
            var lockObj = new object();
            int scanned = 0;
            int found = 0;

            // Use Parallel.ForEach with limited threads
            Parallel.ForEach(ipAddresses,
                new ParallelOptions { MaxDegreeOfParallelism = _maxThreads },
                ipAddress =>
                {
                    var machine = ScanHost(ipAddress);

                    Interlocked.Increment(ref scanned);

                    if (scanned % 25 == 0)
                    {
                        Console.Write($"\rProgress: {scanned}/{ipAddresses.Count}  Found: {found}  ");
                    }

                    if (machine != null)
                    {
                        Interlocked.Increment(ref found);
                        lock (lockObj)
                        {
                            discoveredMachines.Add(machine);
                            _repository.AddOrUpdateMachine(machine);
                        }

                        Console.WriteLine($"\n[+] Found: {ipAddress} ({machine.HostName})");
                    }
                });

            Console.WriteLine($"\rProgress: {scanned}/{ipAddresses.Count}  Found: {found}  ");
            return discoveredMachines;
        }

        /// <summary>
        /// Scan a single host
        /// </summary>
        private MachineInfo ScanHost(string ipAddress)
        {
            // First, ping the host
            if (!PingHost(ipAddress))
                return null;

            // Try to get hostname
            string hostname = GetHostName(ipAddress);

            // Try to get OS information
            var (osVersion, osBuild) = GetOsInfo(ipAddress);

            // If we can't get any info, it might not be a Windows machine
            if (string.IsNullOrEmpty(hostname) && string.IsNullOrEmpty(osVersion))
                return null;

            // Check if PatchinPal client is running
            bool hasClient = CheckClientRunning(ipAddress);

            return new MachineInfo
            {
                IpAddress = ipAddress,
                HostName = hostname ?? ipAddress,
                OsVersion = osVersion ?? "Unknown",
                OsBuild = osBuild ?? "Unknown",
                IsOnline = true,
                LastSeen = DateTime.Now,
                ClientPort = _defaultClientPort,
                Status = hasClient ? UpdateStatus.Unknown : UpdateStatus.Unknown
            };
        }

        /// <summary>
        /// Ping a host to check if it's online
        /// </summary>
        private bool PingHost(string ipAddress)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(ipAddress, _scanTimeout);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get hostname from IP address
        /// </summary>
        private string GetHostName(string ipAddress)
        {
            try
            {
                var hostEntry = Dns.GetHostEntry(ipAddress);
                return hostEntry.HostName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get OS information using WMI (requires admin rights and proper network config)
        /// </summary>
        private (string osVersion, string osBuild) GetOsInfo(string ipAddress)
        {
            try
            {
                var scope = new ManagementScope($"\\\\{ipAddress}\\root\\cimv2");
                scope.Options.Timeout = TimeSpan.FromMilliseconds(_scanTimeout);

                // Try to connect
                scope.Connect();

                var query = new ObjectQuery("SELECT Caption, BuildNumber, Version FROM Win32_OperatingSystem");
                using (var searcher = new ManagementObjectSearcher(scope, query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string caption = obj["Caption"]?.ToString();
                        string buildNumber = obj["BuildNumber"]?.ToString();
                        string version = obj["Version"]?.ToString();

                        return (caption, buildNumber);
                    }
                }
            }
            catch
            {
                // WMI access might fail due to permissions or firewall
                // This is normal for many networks
            }

            return (null, null);
        }

        /// <summary>
        /// Check if PatchinPal client is running on the host
        /// </summary>
        private bool CheckClientRunning(string ipAddress)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(ipAddress, _defaultClientPort, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));

                    if (success)
                    {
                        client.EndConnect(result);
                        return true;
                    }

                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generate list of IP addresses from CIDR notation
        /// </summary>
        private List<string> GenerateIpRange(string cidr)
        {
            var ips = new List<string>();

            // Parse CIDR notation
            string[] parts = cidr.Split('/');
            string baseIp = parts[0];
            int prefixLength = parts.Length > 1 ? int.Parse(parts[1]) : 24;

            // Simple implementation for /24 networks
            if (prefixLength == 24)
            {
                string[] ipParts = baseIp.Split('.');
                string prefix = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";

                for (int i = 1; i < 255; i++)
                {
                    ips.Add($"{prefix}.{i}");
                }
            }
            else if (prefixLength == 16)
            {
                string[] ipParts = baseIp.Split('.');
                string prefix = $"{ipParts[0]}.{ipParts[1]}";

                for (int i = 1; i < 255; i++)
                {
                    for (int j = 1; j < 255; j++)
                    {
                        ips.Add($"{prefix}.{i}.{j}");
                    }
                }
            }
            else
            {
                throw new NotImplementedException($"CIDR prefix /{prefixLength} not implemented. Use /24 or /16.");
            }

            return ips;
        }

        /// <summary>
        /// Refresh status of known machines
        /// </summary>
        public void RefreshMachineStatus()
        {
            var machines = _repository.GetAllMachines();

            Console.WriteLine($"Refreshing status of {machines.Count} machine(s)...");

            foreach (var machine in machines)
            {
                bool isOnline = PingHost(machine.IpAddress);
                machine.IsOnline = isOnline;

                if (isOnline)
                {
                    machine.LastSeen = DateTime.Now;

                    // Update hostname if it changed
                    string hostname = GetHostName(machine.IpAddress);
                    if (!string.IsNullOrEmpty(hostname))
                        machine.HostName = hostname;
                }
            }

            _repository.Save();
            Console.WriteLine("Refresh complete");
        }
    }
}
