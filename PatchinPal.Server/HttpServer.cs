using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using PatchinPal.Common;

namespace PatchinPal.Server
{
    /// <summary>
    /// Simple HTTP server for receiving client reports.
    /// Listens on specified port and accepts POST requests from clients.
    /// Also serves a web dashboard at the root path.
    /// </summary>
    public class HttpServer
    {
        private HttpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private readonly int _port;
        private readonly MachineRepository _repository;
        private readonly NetworkScanner _networkScanner;
        private readonly ClientManager _clientManager;
        private readonly JavaScriptSerializer _serializer;
        private readonly string _apiKey;

        public HttpServer(int port, MachineRepository repository, NetworkScanner networkScanner, ClientManager clientManager)
        {
            _port = port;
            _repository = repository;
            _networkScanner = networkScanner;
            _clientManager = clientManager;
            _serializer = new JavaScriptSerializer();
            _apiKey = ConfigurationManager.AppSettings["ApiKey"] ?? "";
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{_port}/");
                _listener.Start();
                _isRunning = true;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[HTTP Server] Started on port {_port}");
                Console.WriteLine($"[HTTP Server] Dashboard: http://localhost:{_port}/");
                Console.ResetColor();

                _listenerThread = new Thread(ListenForRequests);
                _listenerThread.IsBackground = true;
                _listenerThread.Start();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[HTTP Server] Failed to start: {ex.Message}");
                Console.ResetColor();
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _listener?.Stop();
            _listener?.Close();

            Console.WriteLine("[HTTP Server] Stopped");
        }

        private void ListenForRequests()
        {
            while (_isRunning)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    if (!_isRunning) break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HTTP Server] Listener error: {ex.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            string method = context.Request.HttpMethod;
            string path = context.Request.Url.AbsolutePath;

            try
            {
                if (method == "POST" && path == "/api/clientreport")
                {
                    // Client reports are authenticated by the shared API key in the header
                    if (!ValidateApiKey(context))
                        return;
                    HandleClientReport(context);
                }
                else if (method == "GET" && path == "/api/ping")
                {
                    SendResponse(context, 200, new { status = "online" });
                }
                else if (method == "GET" && path == "/")
                {
                    ServeHtmlDashboard(context);
                }
                else if (method == "GET" && path == "/api/clients")
                {
                    // Read-only endpoint, no auth required for viewing
                    HandleGetClients(context);
                }
                else if (method == "POST" && path == "/api/scan")
                {
                    if (!ValidateApiKey(context))
                        return;
                    HandleScan(context);
                }
                else if (method == "POST" && path == "/api/check")
                {
                    if (!ValidateApiKey(context))
                        return;
                    HandleCheckUpdates(context);
                }
                else if (method == "POST" && path.StartsWith("/api/update/"))
                {
                    if (!ValidateApiKey(context))
                        return;
                    HandleUpdateMachine(context);
                }
                else if (method == "POST" && path.StartsWith("/api/schedule/"))
                {
                    if (!ValidateApiKey(context))
                        return;
                    HandleSchedule(context);
                }
                else if (method == "GET" && path.StartsWith("/api/history/"))
                {
                    HandleGetHistory(context);
                }
                else
                {
                    SendResponse(context, 404, new { error = "Not found" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request handler error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates the X-API-Key header against the configured key.
        /// Returns true if valid or if no key is configured. Sends 403 and returns false otherwise.
        /// </summary>
        private bool ValidateApiKey(HttpListenerContext context)
        {
            if (string.IsNullOrEmpty(_apiKey))
                return true; // Auth disabled if no key configured

            string providedKey = context.Request.Headers["X-API-Key"];
            if (!string.Equals(providedKey, _apiKey, StringComparison.Ordinal))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Auth failed from {context.Request.RemoteEndPoint?.Address}");
                SendResponse(context, 403, new { error = "Invalid or missing API key" });
                return false;
            }
            return true;
        }

        private void HandleClientReport(HttpListenerContext context)
        {
            try
            {
                string requestBody;
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBody = reader.ReadToEnd();
                }

                var report = _serializer.Deserialize<ClientStatusReport>(requestBody);

                var machineInfo = new MachineInfo
                {
                    IpAddress = report.IpAddress,
                    HostName = report.HostName,
                    OsVersion = report.OsVersion,
                    OsBuild = report.OsBuild,
                    IsOnline = true,
                    LastSeen = DateTime.Now,
                    LastUpdateCheck = DateTime.Now,
                    PendingUpdates = report.PendingUpdates,
                    ClientPort = report.ClientPort,
                    Status = report.Status
                };

                _repository.AddOrUpdateMachine(machineInfo);
                _repository.Save();

                SendResponse(context, 200, new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client report: {ex.Message}");
                SendResponse(context, 500, new { error = ex.Message });
            }
        }

        private void SendResponse(HttpListenerContext context, int statusCode, object data)
        {
            try
            {
                string json = _serializer.Serialize(data);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending response: {ex.Message}");
            }
        }

        private void HandleGetClients(HttpListenerContext context)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[API] /api/clients request received");
            Console.ResetColor();

            var machines = _repository.GetAllMachines();

            Console.WriteLine($"[API] Found {machines.Count} machine(s) in repository");
            foreach (var m in machines)
            {
                Console.WriteLine($"  - {m.HostName} ({m.IpAddress}) - Online: {m.IsOnline}, Updates: {m.PendingUpdates}, Status: {m.Status}");
            }

            SendResponse(context, 200, new { machines });
        }

        private void HandleScan(HttpListenerContext context)
        {
            try
            {
                Console.WriteLine("[Web Dashboard] Network scan requested");

                string subnet = GetLocalSubnet();
                Console.WriteLine($"Scanning network: {subnet}");

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        var machines = _networkScanner.ScanSubnet(subnet);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[Web Dashboard] Scan complete! Found {machines.Count} machine(s)");
                        Console.ResetColor();
                        _repository.Save();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[Web Dashboard] Scan error: {ex.Message}");
                        Console.ResetColor();
                    }
                });

                SendResponse(context, 200, new { success = true, message = "Network scan initiated" });
            }
            catch (Exception ex)
            {
                SendResponse(context, 500, new { success = false, error = ex.Message });
            }
        }

        private void HandleCheckUpdates(HttpListenerContext context)
        {
            try
            {
                Console.WriteLine("[Web Dashboard] Check updates requested for all machines");

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        var machines = _repository.GetAllMachines().Where(m => m.IsOnline).ToList();
                        Console.WriteLine($"[Web Dashboard] Checking {machines.Count} online machine(s) for updates...");

                        foreach (var machine in machines)
                        {
                            try
                            {
                                var response = _clientManager.CheckForUpdates(machine.IpAddress);
                                if (response != null && response.Success)
                                {
                                    int updateCount = response.AvailableUpdates?.Count ?? 0;
                                    machine.PendingUpdates = updateCount;
                                    machine.LastUpdateCheck = DateTime.Now;
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"[Web Dashboard] {machine.HostName} ({machine.IpAddress}): {updateCount} update(s) available");
                                    Console.ResetColor();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[Web Dashboard] Error checking {machine.IpAddress}: {ex.Message}");
                                Console.ResetColor();
                            }
                        }

                        _repository.Save();
                        Console.WriteLine("[Web Dashboard] Update check complete");
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[Web Dashboard] Check updates error: {ex.Message}");
                        Console.ResetColor();
                    }
                });

                SendResponse(context, 200, new { success = true, message = "Update check initiated for all machines" });
            }
            catch (Exception ex)
            {
                SendResponse(context, 500, new { success = false, error = ex.Message });
            }
        }

        private void HandleUpdateMachine(HttpListenerContext context)
        {
            try
            {
                string ip = context.Request.Url.AbsolutePath.Substring("/api/update/".Length);
                Console.WriteLine($"[Web Dashboard] Install updates requested for {ip}");

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        var response = _clientManager.InstallUpdates(ip, true);

                        if (response != null && response.Success)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[Web Dashboard] {ip}: {response.Message}");
                            Console.ResetColor();

                            if (response.Status == UpdateStatus.RebootRequired)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[Web Dashboard] {ip} requires reboot");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[Web Dashboard] {ip}: {response?.Message ?? "No response from client"}");
                            Console.ResetColor();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[Web Dashboard] Update error for {ip}: {ex.Message}");
                        Console.ResetColor();
                    }
                });

                SendResponse(context, 200, new { success = true, message = $"Update command sent to {ip}" });
            }
            catch (Exception ex)
            {
                SendResponse(context, 500, new { success = false, error = ex.Message });
            }
        }

        private void HandleSchedule(HttpListenerContext context)
        {
            try
            {
                string ip = context.Request.Url.AbsolutePath.Substring("/api/schedule/".Length);

                DateTime scheduledTime = DateTime.Today.AddDays(1).AddHours(3);
                Console.WriteLine($"[Web Dashboard] Schedule update requested for {ip} at {scheduledTime}");

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        var response = _clientManager.ScheduleUpdate(ip, scheduledTime, false);

                        if (response != null && response.Success)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[Web Dashboard] {ip}: {response.Message}");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[Web Dashboard] {ip}: {response?.Message ?? "No response from client"}");
                            Console.ResetColor();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[Web Dashboard] Schedule error for {ip}: {ex.Message}");
                        Console.ResetColor();
                    }
                });

                SendResponse(context, 200, new { success = true, message = $"Update scheduled for {ip} at {scheduledTime:g}" });
            }
            catch (Exception ex)
            {
                SendResponse(context, 500, new { success = false, error = ex.Message });
            }
        }

        private void HandleGetHistory(HttpListenerContext context)
        {
            // Placeholder for future history feature
            SendResponse(context, 200, new { success = true, history = new List<object>(), message = "History feature coming soon" });
        }

        private void ServeHtmlDashboard(HttpListenerContext context)
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashboard.html");
            string html;

            if (File.Exists(htmlPath))
            {
                html = File.ReadAllText(htmlPath);
            }
            else
            {
                html = "<html><body><h1>Dashboard not found</h1><p>dashboard.html not found at: " + htmlPath + "</p></body></html>";
            }

            byte[] buffer = Encoding.UTF8.GetBytes(html);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        private string GetLocalSubnet()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var ni in interfaces)
                {
                    var properties = ni.GetIPProperties();
                    foreach (var addr in properties.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(addr.Address))
                        {
                            string ip = addr.Address.ToString();
                            if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
                            {
                                string[] parts = ip.Split('.');
                                return $"{parts[0]}.{parts[1]}.{parts[2]}.0/24";
                            }
                        }
                    }
                }
            }
            catch { }

            return "192.168.1.0/24";
        }
    }
}
