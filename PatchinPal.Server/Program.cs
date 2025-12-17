using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using PatchinPal.Common;

namespace PatchinPal.Server
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;

        private static NetworkScanner _networkScanner;
        private static MachineRepository _repository;
        private static ClientManager _clientManager;
        private static HttpServer _httpServer;
        private static ConsoleTrayHelper _trayHelper;
        private static bool _running = true;
        private static Thread _inputThread;

        [STAThread]
        static void Main(string[] args)
        {
            // Parse command line arguments
            bool startMinimized = args.Any(a => a.Equals("/minimized", StringComparison.OrdinalIgnoreCase));

            // If minimized flag, start hidden in tray
            if (startMinimized)
            {
                var handle = GetConsoleWindow();
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_HIDE);
                }
            }

            // Initialize tray helper
            _trayHelper = new ConsoleTrayHelper();
            if (startMinimized)
            {
                _trayHelper.MinimizeToTray();
            }

            // Run main logic
            RunServer();
        }

        static void RunServer()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("    PatchinPal Server v1.0");
            Console.WriteLine("    Central Update Management");
            Console.WriteLine("========================================\n");

            // Initialize components
            _repository = new MachineRepository();
            _networkScanner = new NetworkScanner(_repository);
            _clientManager = new ClientManager(_repository);

            // Load existing data
            _repository.Load();
            Console.WriteLine($"Loaded {_repository.GetAllMachines().Count} machine(s) from database\n");

            // Start HTTP server to receive client reports
            int serverPort = int.Parse(ConfigurationManager.AppSettings["ServerPort"] ?? "8092");
            _httpServer = new HttpServer(serverPort, _repository);
            _httpServer.Start();
            Console.WriteLine();

            // Show help
            ShowHelp();

            // Show prompt
            Console.Write("\nPatchinPal> ");

            // Start command loop in background thread
            _inputThread = new Thread(CommandLoop)
            {
                IsBackground = true,
                Name = "CommandLoop"
            };
            _inputThread.Start();

            // Start Windows Forms message loop for tray icon (on main thread)
            Application.Run(new ApplicationContext());
        }

        static void CommandLoop()
        {
            while (_running)
            {
                // Check if minimized (user manually minimized console)
                _trayHelper?.CheckMinimized();

                if (Console.KeyAvailable)
                {
                    string input = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(input))
                        continue;

                    string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string command = parts[0].ToLower();
                    string[] cmdArgs = parts.Skip(1).ToArray();

                    try
                    {
                        switch (command)
                        {
                            case "scan":
                                ScanNetwork(cmdArgs);
                                break;
                            case "list":
                                ListMachines(cmdArgs);
                                break;
                            case "status":
                                ShowStatus(cmdArgs);
                                break;
                            case "update":
                                UpdateMachine(cmdArgs);
                                break;
                            case "check":
                                CheckForUpdates(cmdArgs);
                                break;
                            case "schedule":
                                ScheduleUpdate(cmdArgs);
                                break;
                            case "help":
                                ShowHelp();
                                break;
                            case "exit":
                            case "quit":
                                _running = false;
                                break;
                            default:
                                Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error: {ex.Message}");
                        Console.ResetColor();
                    }

                    if (_running)
                        Console.Write("\nPatchinPal> ");
                }

                Thread.Sleep(100);
            }

            Console.WriteLine("\nShutting down...");
            _httpServer?.Stop();
            _trayHelper?.Cleanup();
            Console.WriteLine("Exiting...");
            Application.Exit();
            Environment.Exit(0);
        }

        static void ScanNetwork(string[] args)
        {
            string subnet = args.Length > 0 ? args[0] : GetLocalSubnet();

            Console.WriteLine($"Scanning network: {subnet}");
            Console.WriteLine("This may take a few minutes...\n");

            var machines = _networkScanner.ScanSubnet(subnet);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nScan complete! Found {machines.Count} machine(s)");
            Console.ResetColor();

            _repository.Save();
        }

        static void ListMachines(string[] args)
        {
            bool onlineOnly = args.Any(a => a.Equals("-online", StringComparison.OrdinalIgnoreCase));
            var machines = _repository.GetAllMachines();

            if (onlineOnly)
                machines = machines.Where(m => m.IsOnline).ToList();

            if (machines.Count == 0)
            {
                Console.WriteLine("No machines found. Use 'scan' to discover machines.");
                return;
            }

            Console.WriteLine($"\n{"IP Address",-15} {"Hostname",-20} {"OS Version",-30} {"Status",-15} {"Updates"}");
            Console.WriteLine(new string('-', 95));

            foreach (var machine in machines.OrderBy(m => m.IpAddress))
            {
                string status = machine.IsOnline ? "Online" : "Offline";
                ConsoleColor color = machine.IsOnline ? ConsoleColor.Green : ConsoleColor.Gray;

                Console.ForegroundColor = color;
                Console.WriteLine($"{machine.IpAddress,-15} {machine.HostName,-20} {machine.OsVersion,-30} {status,-15} {machine.PendingUpdates}");
                Console.ResetColor();
            }

            Console.WriteLine($"\nTotal: {machines.Count} | Online: {machines.Count(m => m.IsOnline)} | Offline: {machines.Count(m => !m.IsOnline)}");
        }

        static void ShowStatus(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: status <ip-address>");
                return;
            }

            string ipAddress = args[0];
            var machine = _repository.GetMachine(ipAddress);

            if (machine == null)
            {
                Console.WriteLine($"Machine {ipAddress} not found in database.");
                return;
            }

            Console.WriteLine($"\n--- Machine Status: {ipAddress} ---");
            Console.WriteLine($"Hostname: {machine.HostName}");
            Console.WriteLine($"OS Version: {machine.OsVersion}");
            Console.WriteLine($"OS Build: {machine.OsBuild}");
            Console.WriteLine($"Status: {(machine.IsOnline ? "Online" : "Offline")}");
            Console.WriteLine($"Last Seen: {machine.LastSeen}");
            Console.WriteLine($"Pending Updates: {machine.PendingUpdates}");
            Console.WriteLine($"Update Status: {machine.Status}");

            if (machine.IsOnline)
            {
                Console.WriteLine("\nFetching live status from client...");
                var response = _clientManager.GetClientStatus(ipAddress);

                if (response != null && response.Success)
                {
                    Console.WriteLine($"Available Updates: {response.AvailableUpdates?.Count ?? 0}");

                    if (response.AvailableUpdates != null && response.AvailableUpdates.Count > 0)
                    {
                        Console.WriteLine("\nAvailable Updates:");
                        foreach (var update in response.AvailableUpdates)
                        {
                            Console.WriteLine($"  [{update.Severity}] {update.Title}");
                        }
                    }
                }
            }
        }

        static void UpdateMachine(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: update <ip-address> [-aggressive]");
                return;
            }

            string ipAddress = args[0];
            bool aggressive = args.Any(a => a.Equals("-aggressive", StringComparison.OrdinalIgnoreCase));

            Console.WriteLine($"Sending update command to {ipAddress}...");

            var response = _clientManager.InstallUpdates(ipAddress, aggressive);

            if (response != null && response.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Success: {response.Message}");
                Console.ResetColor();

                if (response.Status == UpdateStatus.RebootRequired)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Machine requires reboot.");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed: {response?.Message ?? "No response from client"}");
                Console.ResetColor();
            }
        }

        static void CheckForUpdates(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: check <ip-address|all>");
                return;
            }

            if (args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var machines = _repository.GetAllMachines().Where(m => m.IsOnline).ToList();
                Console.WriteLine($"Checking {machines.Count} online machine(s) for updates...\n");

                foreach (var machine in machines)
                {
                    Console.Write($"{machine.IpAddress} ({machine.HostName})... ");
                    var response = _clientManager.CheckForUpdates(machine.IpAddress);

                    if (response != null && response.Success)
                    {
                        int count = response.AvailableUpdates?.Count ?? 0;
                        machine.PendingUpdates = count;
                        machine.Status = count > 0 ? UpdateStatus.UpdatesAvailable : UpdateStatus.UpToDate;

                        Console.ForegroundColor = count > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
                        Console.WriteLine($"{count} update(s)");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Failed");
                        Console.ResetColor();
                    }
                }

                _repository.Save();
            }
            else
            {
                string ipAddress = args[0];
                Console.WriteLine($"Checking {ipAddress} for updates...");

                var response = _clientManager.CheckForUpdates(ipAddress);

                if (response != null && response.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Found {response.AvailableUpdates?.Count ?? 0} update(s)");
                    Console.ResetColor();

                    if (response.AvailableUpdates != null && response.AvailableUpdates.Count > 0)
                    {
                        foreach (var update in response.AvailableUpdates)
                        {
                            Console.WriteLine($"  [{update.Severity}] {update.Title}");
                        }
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Failed to check for updates");
                    Console.ResetColor();
                }
            }
        }

        static void ScheduleUpdate(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: schedule <ip-address> <time> [-aggressive]");
                Console.WriteLine("Time format: yyyy-MM-dd HH:mm or HH:mm (today)");
                Console.WriteLine("Example: schedule 192.168.1.100 23:00");
                return;
            }

            string ipAddress = args[0];
            string timeStr = args[1];
            bool aggressive = args.Any(a => a.Equals("-aggressive", StringComparison.OrdinalIgnoreCase));

            DateTime scheduledTime;

            // Try to parse time
            if (timeStr.Contains("-"))
            {
                // Full date-time
                if (!DateTime.TryParse(timeStr, out scheduledTime))
                {
                    Console.WriteLine("Invalid date/time format");
                    return;
                }
            }
            else
            {
                // Time only, assume today
                if (!DateTime.TryParse($"{DateTime.Now:yyyy-MM-dd} {timeStr}", out scheduledTime))
                {
                    Console.WriteLine("Invalid time format");
                    return;
                }
            }

            Console.WriteLine($"Scheduling update for {ipAddress} at {scheduledTime}...");

            var response = _clientManager.ScheduleUpdate(ipAddress, scheduledTime, aggressive);

            if (response != null && response.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Success: {response.Message}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed: {response?.Message ?? "No response"}");
                Console.ResetColor();
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("\nAvailable Commands:");
            Console.WriteLine("  scan [subnet]          - Scan network for machines (default: local subnet)");
            Console.WriteLine("  list [-online]         - List all discovered machines");
            Console.WriteLine("  status <ip>            - Show detailed status of a machine");
            Console.WriteLine("  check <ip|all>         - Check for available updates");
            Console.WriteLine("  update <ip> [-aggressive] - Install updates on a machine");
            Console.WriteLine("  schedule <ip> <time> [-aggressive] - Schedule an update");
            Console.WriteLine("  help                   - Show this help");
            Console.WriteLine("  exit                   - Exit the program");
        }

        static string GetLocalSubnet()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        string ipStr = ip.ToString();
                        int lastDot = ipStr.LastIndexOf('.');
                        return ipStr.Substring(0, lastDot) + ".0/24";
                    }
                }
            }
            catch { }

            return "192.168.1.0/24";
        }
    }
}
