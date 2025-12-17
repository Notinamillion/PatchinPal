using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace PatchinPal.Client
{
    /// <summary>
    /// Auto-detects PatchinPal Server on the network
    /// </summary>
    public static class ServerAutoDetect
    {
        private const int DEFAULT_SERVER_PORT = 8091;
        private const int TIMEOUT_MS = 1000;

        /// <summary>
        /// Try to find the PatchinPal Server on the network
        /// </summary>
        /// <returns>Server IP address if found, null otherwise</returns>
        public static string FindServer()
        {
            Console.WriteLine("Auto-detecting PatchinPal Server...");

            // Get list of IP addresses to try
            var candidateIps = GetCandidateServers();

            foreach (var ip in candidateIps)
            {
                if (TryConnectToServer(ip, DEFAULT_SERVER_PORT))
                {
                    Console.WriteLine($"Found server at {ip}:{DEFAULT_SERVER_PORT}");
                    return ip;
                }
            }

            Console.WriteLine("No server found on network");
            return null;
        }

        /// <summary>
        /// Get list of likely server IP addresses
        /// </summary>
        private static List<string> GetCandidateServers()
        {
            var candidates = new List<string>();

            try
            {
                // Get local IP address
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipStr = ip.ToString();
                        string[] parts = ipStr.Split('.');

                        if (parts.Length == 4)
                        {
                            // Try gateway addresses first (most common for servers)
                            string subnet = $"{parts[0]}.{parts[1]}.{parts[2]}";

                            // Common server/gateway addresses
                            candidates.Add($"{subnet}.1");   // Router/Gateway
                            candidates.Add($"{subnet}.10");  // Common server IP
                            candidates.Add($"{subnet}.100"); // Common server IP
                            candidates.Add($"{subnet}.120"); // Your specific server
                            candidates.Add($"{subnet}.200"); // Common server IP

                            // Add a few more IPs in the subnet
                            for (int i = 2; i <= 254; i += 10)
                            {
                                string testIp = $"{subnet}.{i}";
                                if (!candidates.Contains(testIp))
                                {
                                    candidates.Add(testIp);
                                }
                            }
                        }

                        break; // Only use first valid IPv4 address
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting candidate servers: {ex.Message}");
            }

            return candidates;
        }

        /// <summary>
        /// Try to connect to server at specified address
        /// </summary>
        private static bool TryConnectToServer(string ipAddress, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(ipAddress, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(TIMEOUT_MS));

                    if (success)
                    {
                        try
                        {
                            client.EndConnect(result);

                            // Try to verify it's actually our server by sending HTTP GET to /api/ping
                            return VerifyServerIdentity(ipAddress, port);
                        }
                        catch
                        {
                            return false;
                        }
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
        /// Verify that the server is actually PatchinPal Server
        /// </summary>
        private static bool VerifyServerIdentity(string ipAddress, int port)
        {
            try
            {
                string url = $"http://{ipAddress}:{port}/api/ping";
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = TIMEOUT_MS;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    // If we get a 200 OK response, it's likely our server
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
