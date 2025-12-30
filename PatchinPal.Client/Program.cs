using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PatchinPal.Common;

namespace PatchinPal.Client
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        private static HttpServer _httpServer;
        private static UpdateManager _updateManager;
        private static ScheduleManager _scheduleManager;
        private static ServerReporter _serverReporter;
        private static bool _isRunning = true;

        [STAThread]
        static void Main(string[] args)
        {
            // Check for admin rights
            if (!IsAdministrator())
            {
                MessageBox.Show(
                    "PatchinPal Client requires Administrator privileges.\n\nPlease run as Administrator.",
                    "Administrator Rights Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Parse command line arguments
            bool consoleMode = args.Any(a => a.Equals("/console", StringComparison.OrdinalIgnoreCase));
            bool backgroundMode = args.Any(a => a.Equals("/background", StringComparison.OrdinalIgnoreCase));

            // Handle different modes
            if (consoleMode)
            {
                // Run in console mode (for debugging/manual control)
                AllocConsole();
                StartConsoleMode(args);
            }
            else if (args.Length > 0 && !backgroundMode)
            {
                // Handle one-time commands
                AllocConsole();
                HandleCommandLine(args);
            }
            else
            {
                // Run in tray mode (default) - hide console window
                var handle = GetConsoleWindow();
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_HIDE);
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApplicationContext());
            }
        }

        static void StartConsoleMode(string[] args)
        {
            try
            {
                // Show banner
                ConsoleBanner.Show();

                int port = int.Parse(ConfigurationManager.AppSettings["ListenPort"] ?? "8090");

                Console.WriteLine($"Starting PatchinPal Client on port {port}...\n");

                _updateManager = new UpdateManager();
                _scheduleManager = new ScheduleManager(_updateManager);
                _serverReporter = new ServerReporter(_updateManager);
                _httpServer = new HttpServer(port, _updateManager, _scheduleManager);

                _httpServer.Start();
                _scheduleManager.Start();
                _serverReporter.Start();

                Console.WriteLine("Client is running. Commands:");
                Console.WriteLine("  check   - Check for updates now");
                Console.WriteLine("  install - Install available updates");
                Console.WriteLine("  status  - Show current status");
                Console.WriteLine("  server <ip> <port> <interval> - Configure server reporting");
                Console.WriteLine("  report  - Report to server now");
                Console.WriteLine("  exit    - Stop the client\n");

                // Command loop
                while (_isRunning)
                {
                    Console.Write("> ");
                    string input = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(input)) continue;

                    string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string command = parts[0].ToLower();

                    switch (command)
                    {
                        case "check":
                            CheckForUpdates();
                            break;
                        case "install":
                            InstallUpdates(false);
                            break;
                        case "install-aggressive":
                            InstallUpdates(true);
                            break;
                        case "status":
                            ShowStatus();
                            break;
                        case "server":
                            ConfigureServer(parts);
                            break;
                        case "report":
                            _serverReporter?.ReportNow();
                            break;
                        case "exit":
                        case "quit":
                            _isRunning = false;
                            break;
                        default:
                            Console.WriteLine("Unknown command. Type 'exit' to quit.");
                            break;
                    }
                }

                Console.WriteLine("\nShutting down...");
                _httpServer?.Stop();
                _scheduleManager?.Stop();
                _serverReporter?.Stop();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }

        static void HandleCommandLine(string[] args)
        {
            ConsoleBanner.ShowSmall();

            string command = args[0].ToLower();
            _updateManager = new UpdateManager();

            switch (command)
            {
                case "check":
                    CheckForUpdates();
                    break;
                case "install":
                    bool aggressive = args.Any(a => a.Equals("-aggressive", StringComparison.OrdinalIgnoreCase));
                    InstallUpdates(aggressive);
                    break;
                case "status":
                    ShowStatus();
                    break;
                case "service":
                case "console":
                    StartConsoleMode(args);
                    break;
                default:
                    ShowHelp();
                    break;
            }
        }

        static void CheckForUpdates()
        {
            Console.WriteLine("\nChecking for updates...");
            var updates = _updateManager.CheckForUpdates();

            if (updates.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("System is up to date!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n{updates.Count} update(s) available:");
                Console.ResetColor();

                foreach (var update in updates)
                {
                    Console.WriteLine($"  [{update.Severity}] {update.Title}");
                    if (!string.IsNullOrEmpty(update.KbArticleId))
                        Console.WriteLine($"      KB: {update.KbArticleId}");
                }
            }
            Console.WriteLine();
        }

        static void InstallUpdates(bool aggressive)
        {
            Console.WriteLine($"\nInstalling updates{(aggressive ? " (AGGRESSIVE MODE)" : "")}...");
            var result = _updateManager.InstallUpdates(aggressive);

            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Updates installed successfully!");
                Console.ResetColor();

                if (result.Status == UpdateStatus.RebootRequired)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("A system reboot is required.");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Installation failed: {result.Message}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        static void ShowStatus()
        {
            var status = _updateManager.GetStatus();
            Console.WriteLine("\n--- System Status ---");
            Console.WriteLine($"Status: {status.Status}");
            Console.WriteLine($"Pending Updates: {status.AvailableUpdates?.Count ?? 0}");
            Console.WriteLine($"Last Check: {status.Timestamp}");
            Console.WriteLine();
        }

        static void ShowHelp()
        {
            Console.WriteLine("PatchinPal Client - Usage:");
            Console.WriteLine("  PatchinPal.Client.exe service              - Run in service mode (listens for server commands)");
            Console.WriteLine("  PatchinPal.Client.exe check                - Check for updates");
            Console.WriteLine("  PatchinPal.Client.exe install              - Install updates");
            Console.WriteLine("  PatchinPal.Client.exe install -aggressive  - Install updates aggressively");
            Console.WriteLine("  PatchinPal.Client.exe status               - Show update status");
        }

        static void ConfigureServer(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: server <ip> <port> <interval>");
                Console.WriteLine("Example: server 192.168.1.50 8091 10");
                Console.WriteLine("To disable: server \"\" 0 0");
                return;
            }

            string serverIp = args[1];
            if (!int.TryParse(args[2], out int port))
            {
                Console.WriteLine("Invalid port number");
                return;
            }

            if (!int.TryParse(args[3], out int interval))
            {
                Console.WriteLine("Invalid interval");
                return;
            }

            try
            {
                _serverReporter?.SetServer(serverIp, port, interval);
                Console.WriteLine("Server configuration saved and applied.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error configuring server: {ex.Message}");
                Console.ResetColor();
            }
        }

        static bool IsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
