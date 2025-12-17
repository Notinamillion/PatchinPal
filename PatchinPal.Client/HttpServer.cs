using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using PatchinPal.Common;

namespace PatchinPal.Client
{
    /// <summary>
    /// Lightweight HTTP server to receive commands from the PatchinPal Server
    /// </summary>
    public class HttpServer
    {
        private HttpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private readonly int _port;
        private readonly UpdateManager _updateManager;
        private readonly ScheduleManager _scheduleManager;
        private readonly JavaScriptSerializer _serializer;

        public HttpServer(int port, UpdateManager updateManager, ScheduleManager scheduleManager)
        {
            _port = port;
            _updateManager = updateManager;
            _scheduleManager = scheduleManager;
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

                Console.WriteLine($"HTTP Server listening on port {_port}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to start HTTP server: {ex.Message}");
                Console.WriteLine("Make sure you're running as Administrator.");
                Console.ResetColor();
                throw;
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

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {method} {path}");

                if (method == "POST" && path == "/api/command")
                {
                    HandleCommand(context);
                }
                else if (method == "GET" && path == "/api/status")
                {
                    HandleStatusRequest(context);
                }
                else if (method == "GET" && path == "/api/ping")
                {
                    HandlePing(context);
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

        private void HandlePing(HttpListenerContext context)
        {
            SendResponse(context, 200, new
            {
                status = "online",
                hostname = Environment.MachineName,
                timestamp = DateTime.Now
            });
        }

        private void HandleStatusRequest(HttpListenerContext context)
        {
            var status = _updateManager.GetStatus();
            SendResponse(context, 200, status);
        }

        private void HandleCommand(HttpListenerContext context)
        {
            // Read command from request body
            string requestBody;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                requestBody = reader.ReadToEnd();
            }

            var command = _serializer.Deserialize<UpdateCommand>(requestBody);
            UpdateResponse response;

            switch (command.Type)
            {
                case CommandType.CheckForUpdates:
                    Console.WriteLine("Executing: Check for updates");
                    var updates = _updateManager.CheckForUpdates();
                    response = new UpdateResponse
                    {
                        Success = true,
                        Message = $"Found {updates.Count} update(s)",
                        AvailableUpdates = updates,
                        Status = updates.Count > 0 ? UpdateStatus.UpdatesAvailable : UpdateStatus.UpToDate,
                        Timestamp = DateTime.Now
                    };
                    break;

                case CommandType.InstallUpdates:
                    Console.WriteLine($"Executing: Install updates (Aggressive: {command.AggressiveMode})");
                    response = _updateManager.InstallUpdates(command.AggressiveMode);
                    break;

                case CommandType.ScheduleUpdate:
                    Console.WriteLine($"Executing: Schedule update for {command.ScheduledTime}");
                    if (command.ScheduledTime.HasValue)
                    {
                        _scheduleManager.ScheduleUpdate(command.ScheduledTime.Value, command.AggressiveMode);
                        response = new UpdateResponse
                        {
                            Success = true,
                            Message = $"Update scheduled for {command.ScheduledTime.Value}",
                            Status = UpdateStatus.Unknown,
                            Timestamp = DateTime.Now
                        };
                    }
                    else
                    {
                        response = new UpdateResponse
                        {
                            Success = false,
                            Message = "No scheduled time provided",
                            Status = UpdateStatus.Unknown,
                            Timestamp = DateTime.Now
                        };
                    }
                    break;

                case CommandType.GetStatus:
                    Console.WriteLine("Executing: Get status");
                    response = _updateManager.GetStatus();
                    break;

                case CommandType.CancelScheduledUpdate:
                    Console.WriteLine("Executing: Cancel scheduled update");
                    _scheduleManager.CancelScheduledUpdate();
                    response = new UpdateResponse
                    {
                        Success = true,
                        Message = "Scheduled update cancelled",
                        Status = UpdateStatus.Unknown,
                        Timestamp = DateTime.Now
                    };
                    break;

                default:
                    response = new UpdateResponse
                    {
                        Success = false,
                        Message = $"Unknown command: {command.Type}",
                        Status = UpdateStatus.Unknown,
                        Timestamp = DateTime.Now
                    };
                    break;
            }

            SendResponse(context, 200, response);
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
