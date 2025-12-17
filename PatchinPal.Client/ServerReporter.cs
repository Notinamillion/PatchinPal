using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using PatchinPal.Common;

namespace PatchinPal.Client
{
    /// <summary>
    /// Periodically reports client status to the central server
    /// </summary>
    public class ServerReporter
    {
        private readonly UpdateManager _updateManager;
        private readonly JavaScriptSerializer _serializer;
        private Timer _reportTimer;
        private bool _isRunning;
        private string _serverAddress;
        private int _serverPort;
        private int _reportInterval;
        private bool _isEnabled;

        public ServerReporter(UpdateManager updateManager)
        {
            _updateManager = updateManager;
            _serializer = new JavaScriptSerializer();
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            _serverAddress = ConfigurationManager.AppSettings["ServerAddress"] ?? "";
            _serverPort = int.Parse(ConfigurationManager.AppSettings["ServerPort"] ?? "8091");
            _reportInterval = int.Parse(ConfigurationManager.AppSettings["ReportInterval"] ?? "10");
            _isEnabled = bool.Parse(ConfigurationManager.AppSettings["EnableServerReporting"] ?? "false");
        }

        public void Start()
        {
            if (!_isEnabled)
            {
                Console.WriteLine("Server reporting is disabled");
                return;
            }

            if (string.IsNullOrWhiteSpace(_serverAddress))
            {
                Console.WriteLine("Server address not configured. Reporting disabled.");
                return;
            }

            _isRunning = true;

            // Report immediately on startup
            ReportToServer();

            // Set up periodic reporting
            int intervalMs = _reportInterval * 60 * 1000;
            _reportTimer = new Timer(
                _ => ReportToServer(),
                null,
                intervalMs,
                intervalMs
            );

            Console.WriteLine($"Server reporting enabled: {_serverAddress}:{_serverPort} every {_reportInterval} minutes");
        }

        public void Stop()
        {
            _isRunning = false;
            _reportTimer?.Dispose();
            Console.WriteLine("Server reporting stopped");
        }

        /// <summary>
        /// Manually trigger a report to the server
        /// </summary>
        public void ReportNow()
        {
            ReportToServer();
        }

        /// <summary>
        /// Update server configuration at runtime
        /// </summary>
        public void SetServer(string address, int port, int intervalMinutes)
        {
            bool wasRunning = _isRunning;

            if (wasRunning)
                Stop();

            _serverAddress = address;
            _serverPort = port;
            _reportInterval = intervalMinutes;
            _isEnabled = !string.IsNullOrWhiteSpace(address);

            // Update config file
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["ServerAddress"].Value = address;
            config.AppSettings.Settings["ServerPort"].Value = port.ToString();
            config.AppSettings.Settings["ReportInterval"].Value = intervalMinutes.ToString();
            config.AppSettings.Settings["EnableServerReporting"].Value = _isEnabled.ToString().ToLower();
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");

            Console.WriteLine($"Server configuration updated: {address}:{port}");

            if (wasRunning && _isEnabled)
                Start();
        }

        private void ReportToServer()
        {
            if (!_isRunning || string.IsNullOrWhiteSpace(_serverAddress))
                return;

            try
            {
                // Get current status
                var status = _updateManager.GetStatus();

                // Gather system information
                var report = new ClientStatusReport
                {
                    IpAddress = GetLocalIpAddress(),
                    HostName = Environment.MachineName,
                    OsVersion = GetOsVersion(),
                    OsBuild = GetOsBuild(),
                    IsOnline = true,
                    LastSeen = DateTime.Now,
                    LastUpdateCheck = DateTime.Now,
                    PendingUpdates = status.AvailableUpdates?.Count ?? 0,
                    ClientPort = int.Parse(ConfigurationManager.AppSettings["ListenPort"] ?? "8090"),
                    Status = status.Status,
                    AvailableUpdates = status.AvailableUpdates
                };

                // Send to server
                SendReport(report);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Failed to report to server: {ex.Message}");
            }
        }

        private void SendReport(ClientStatusReport report)
        {
            try
            {
                string url = $"http://{_serverAddress}:{_serverPort}/api/clientreport";
                string json = _serializer.Serialize(report);

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 5000; // 5 seconds

                byte[] data = Encoding.UTF8.GetBytes(json);
                request.ContentLength = data.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Status reported to server successfully");
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ConnectFailure)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Cannot reach server at {_serverAddress}:{_serverPort}");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Server communication error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error sending report: {ex.Message}");
            }
        }

        private string GetLocalIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }

        private string GetOsVersion()
        {
            try
            {
                // Read from Registry for accurate Windows 10/11 detection
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        string productName = key.GetValue("ProductName")?.ToString() ?? "";

                        // If we have a product name from registry, use it
                        if (!string.IsNullOrEmpty(productName))
                        {
                            return productName;
                        }
                    }
                }

                // Fallback to Environment.OSVersion
                var os = Environment.OSVersion;
                return $"Windows {os.Version.Major}.{os.Version.Minor}";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetOsBuild()
        {
            try
            {
                // Try to get build number from Registry first (more accurate)
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        // Try CurrentBuild first, then CurrentBuildNumber
                        string build = key.GetValue("CurrentBuild")?.ToString();
                        if (string.IsNullOrEmpty(build))
                        {
                            build = key.GetValue("CurrentBuildNumber")?.ToString();
                        }

                        // Get UBR (Update Build Revision) for complete build number
                        string ubr = key.GetValue("UBR")?.ToString();

                        if (!string.IsNullOrEmpty(build))
                        {
                            if (!string.IsNullOrEmpty(ubr))
                            {
                                return $"{build}.{ubr}";
                            }
                            return build;
                        }
                    }
                }

                // Fallback to Environment.OSVersion
                return Environment.OSVersion.Version.Build.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
