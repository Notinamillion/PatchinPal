using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using PatchinPal.Common;

namespace PatchinPal.Server
{
    /// <summary>
    /// HTTP server to receive status reports from clients
    /// </summary>
    public class HttpServer
    {
        private HttpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private readonly int _port;
        private readonly MachineRepository _repository;
        private readonly JavaScriptSerializer _serializer;

        public HttpServer(int port, MachineRepository repository)
        {
            _port = port;
            _repository = repository;
            _serializer = new JavaScriptSerializer();
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{_port}/");
                _listener.Start();
                _isRunning = true;

                _listenerThread = new Thread(ListenLoop);
                _listenerThread.IsBackground = true;
                _listenerThread.Start();

                Console.WriteLine($"Server HTTP listener started on port {_port}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to start HTTP server: {ex.Message}");
                Console.WriteLine("Client auto-reporting will not work, but manual commands will still function.");
                Console.ResetColor();
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            _listener?.Close();
        }

        private void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Listener error: {ex.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath.ToLower();
                string method = context.Request.HttpMethod.ToUpper();

                if (method == "POST" && path == "/api/clientreport")
                {
                    HandleClientReport(context);
                }
                else if (method == "GET" && path == "/api/ping")
                {
                    SendResponse(context, 200, new { status = "online" });
                }
                else
                {
                    SendResponse(context, 404, new { error = "Not found" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request handler error: {ex.Message}");
                SendResponse(context, 500, new { error = ex.Message });
            }
        }

        private void HandleClientReport(HttpListenerContext context)
        {
            try
            {
                // Read report from request body
                string requestBody;
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBody = reader.ReadToEnd();
                }

                var report = _serializer.Deserialize<ClientStatusReport>(requestBody);

                // Convert to MachineInfo and store
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

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Report received from {report.HostName} ({report.IpAddress}) - {report.PendingUpdates} updates pending");

                SendResponse(context, 200, new { success = true, message = "Report received" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing client report: {ex.Message}");
                SendResponse(context, 500, new { success = false, error = ex.Message });
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
    }
}
