using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using PatchinPal.Common;

namespace PatchinPal.Server
{
    /// <summary>
    /// Manages HTTP communication with PatchinPal clients
    /// </summary>
    public class ClientManager
    {
        private readonly MachineRepository _repository;
        private readonly JavaScriptSerializer _serializer;
        private readonly int _timeout = 30000; // 30 seconds

        public ClientManager(MachineRepository repository)
        {
            _repository = repository;
            _serializer = new JavaScriptSerializer();
        }

        /// <summary>
        /// Get status from a client
        /// </summary>
        public UpdateResponse GetClientStatus(string ipAddress)
        {
            var machine = _repository.GetMachine(ipAddress);
            if (machine == null)
            {
                Console.WriteLine($"Machine {ipAddress} not found in database");
                return null;
            }

            try
            {
                string url = $"http://{ipAddress}:{machine.ClientPort}/api/status";
                return SendGetRequest(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting status from {ipAddress}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check for updates on a client
        /// </summary>
        public UpdateResponse CheckForUpdates(string ipAddress)
        {
            return SendCommand(ipAddress, new UpdateCommand
            {
                Type = CommandType.CheckForUpdates
            });
        }

        /// <summary>
        /// Install updates on a client
        /// </summary>
        public UpdateResponse InstallUpdates(string ipAddress, bool aggressive)
        {
            return SendCommand(ipAddress, new UpdateCommand
            {
                Type = CommandType.InstallUpdates,
                AggressiveMode = aggressive
            });
        }

        /// <summary>
        /// Schedule an update on a client
        /// </summary>
        public UpdateResponse ScheduleUpdate(string ipAddress, DateTime scheduledTime, bool aggressive)
        {
            return SendCommand(ipAddress, new UpdateCommand
            {
                Type = CommandType.ScheduleUpdate,
                ScheduledTime = scheduledTime,
                AggressiveMode = aggressive
            });
        }

        /// <summary>
        /// Cancel a scheduled update on a client
        /// </summary>
        public UpdateResponse CancelScheduledUpdate(string ipAddress)
        {
            return SendCommand(ipAddress, new UpdateCommand
            {
                Type = CommandType.CancelScheduledUpdate
            });
        }

        /// <summary>
        /// Ping a client to check if it's online
        /// </summary>
        public bool PingClient(string ipAddress)
        {
            var machine = _repository.GetMachine(ipAddress);
            if (machine == null)
                return false;

            try
            {
                string url = $"http://{ipAddress}:{machine.ClientPort}/api/ping";
                var response = SendGetRequest(url);
                return response != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Send a command to a client
        /// </summary>
        private UpdateResponse SendCommand(string ipAddress, UpdateCommand command)
        {
            var machine = _repository.GetMachine(ipAddress);
            if (machine == null)
            {
                Console.WriteLine($"Machine {ipAddress} not found in database");
                return null;
            }

            try
            {
                string url = $"http://{ipAddress}:{machine.ClientPort}/api/command";
                string json = _serializer.Serialize(command);

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = _timeout;

                byte[] data = Encoding.UTF8.GetBytes(json);
                request.ContentLength = data.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseJson = reader.ReadToEnd();
                    return _serializer.Deserialize<UpdateResponse>(responseJson);
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        string error = reader.ReadToEnd();
                        Console.WriteLine($"Server error from {ipAddress}: {error}");
                    }
                }
                else
                {
                    Console.WriteLine($"Connection error to {ipAddress}: {ex.Message}");
                }

                // Mark machine as offline
                machine.IsOnline = false;
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending command to {ipAddress}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Send a GET request to a client
        /// </summary>
        private UpdateResponse SendGetRequest(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = _timeout;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseJson = reader.ReadToEnd();
                    return _serializer.Deserialize<UpdateResponse>(responseJson);
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.Timeout)
                {
                    Console.WriteLine("Request timed out");
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET request: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Batch update multiple clients
        /// </summary>
        public void UpdateMultipleClients(string[] ipAddresses, bool aggressive)
        {
            Console.WriteLine($"Updating {ipAddresses.Length} client(s)...\n");

            foreach (var ip in ipAddresses)
            {
                Console.Write($"{ip}... ");
                var response = InstallUpdates(ip, aggressive);

                if (response != null && response.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Success");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Failed");
                    Console.ResetColor();
                }
            }
        }
    }
}
