using System;
using System.Configuration;
using System.Drawing;
using System.Windows.Forms;
using PatchinPal.Common;

namespace PatchinPal.Client
{
    /// <summary>
    /// System tray application context for PatchinPal Client
    /// </summary>
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private UpdateManager _updateManager;
        private ScheduleManager _scheduleManager;
        private ServerReporter _serverReporter;
        private HttpServer _httpServer;
        private bool _isFirstRun;

        public TrayApplicationContext()
        {
            Application.ApplicationExit += OnApplicationExit;

            // Check if this is first run
            _isFirstRun = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ServerAddress"]);

            // Initialize components
            InitializeComponents();

            // Create tray icon
            CreateTrayIcon();

            // Handle first run
            if (_isFirstRun)
            {
                HandleFirstRun();
            }
            else
            {
                // Normal startup
                ShowBalloonTip("PatchinPal Client Started", "Running in background. Right-click icon for options.", ToolTipIcon.Info);
            }
        }

        private void InitializeComponents()
        {
            try
            {
                int port = int.Parse(ConfigurationManager.AppSettings["ListenPort"] ?? "8090");

                _updateManager = new UpdateManager();
                _scheduleManager = new ScheduleManager(_updateManager);
                _serverReporter = new ServerReporter(_updateManager);
                _httpServer = new HttpServer(port, _updateManager, _scheduleManager);

                _httpServer.Start();
                _scheduleManager.Start();
                _serverReporter.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing PatchinPal Client: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateTrayIcon()
        {
            // Create context menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Check for Updates", null, OnCheckUpdates);
            contextMenu.Items.Add("Install Updates", null, OnInstallUpdates);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("View Status", null, OnViewStatus);
            contextMenu.Items.Add("Configure Server...", null, OnConfigureServer);
            contextMenu.Items.Add(new ToolStripSeparator());

            var autoStartItem = new ToolStripMenuItem("Run at Startup");
            autoStartItem.Checked = StartupManager.IsStartupEnabled();
            autoStartItem.Click += OnToggleAutoStart;
            contextMenu.Items.Add(autoStartItem);

            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, OnExit);

            // Create tray icon (using default application icon)
            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // We'll use a default icon
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = "PatchinPal Client"
            };

            _trayIcon.DoubleClick += OnViewStatus;
        }

        private void HandleFirstRun()
        {
            ShowBalloonTip("First Run Setup", "Detecting server on network...", ToolTipIcon.Info);

            // Try to auto-detect server
            string serverIp = ServerAutoDetect.FindServer();

            if (!string.IsNullOrEmpty(serverIp))
            {
                // Found server, configure it
                var result = MessageBox.Show(
                    $"Found PatchinPal Server at {serverIp}:8091\n\n" +
                    "Would you like to connect to this server and enable auto-start?",
                    "Server Detected",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    ConfigureServer(serverIp, 8091, 10);
                    StartupManager.EnableStartup();
                    ShowBalloonTip("Configuration Complete", "Client configured and will start automatically with Windows.", ToolTipIcon.Info);
                }
            }
            else
            {
                // Didn't find server, ask user to configure manually
                MessageBox.Show(
                    "Could not auto-detect PatchinPal Server on the network.\n\n" +
                    "Please use 'Configure Server' from the tray menu to set up manually.",
                    "Setup Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void OnCheckUpdates(object sender, EventArgs e)
        {
            try
            {
                var updates = _updateManager.CheckForUpdates();

                if (updates.Count == 0)
                {
                    ShowBalloonTip("No Updates", "Your system is up to date.", ToolTipIcon.Info);
                }
                else
                {
                    ShowBalloonTip("Updates Available", $"{updates.Count} update(s) available. Right-click to install.", ToolTipIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking for updates: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnInstallUpdates(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Install available updates now?\n\nThis may require a system reboot.",
                "Install Updates",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    ShowBalloonTip("Installing Updates", "Update installation in progress...", ToolTipIcon.Info);
                    var response = _updateManager.InstallUpdates(false);

                    if (response.Success)
                    {
                        if (response.Status == UpdateStatus.RebootRequired)
                        {
                            ShowBalloonTip("Updates Installed", "Updates installed successfully. Reboot required.", ToolTipIcon.Info);
                        }
                        else
                        {
                            ShowBalloonTip("Updates Installed", "Updates installed successfully.", ToolTipIcon.Info);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Update installation failed: {response.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error installing updates: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnViewStatus(object sender, EventArgs e)
        {
            try
            {
                var status = _updateManager.GetStatus();
                string message = $"Status: {status.Status}\n" +
                               $"Pending Updates: {status.AvailableUpdates?.Count ?? 0}\n" +
                               $"Last Check: {status.Timestamp:g}\n\n";

                if (status.AvailableUpdates != null && status.AvailableUpdates.Count > 0)
                {
                    message += "Available Updates:\n";
                    foreach (var update in status.AvailableUpdates)
                    {
                        message += $"- {update.Title}\n";
                    }
                }

                MessageBox.Show(message, "PatchinPal Client Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting status: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnConfigureServer(object sender, EventArgs e)
        {
            // Simple input dialog for server configuration
            string currentServer = ConfigurationManager.AppSettings["ServerAddress"] ?? "";
            string currentPort = ConfigurationManager.AppSettings["ServerPort"] ?? "8091";
            string currentInterval = ConfigurationManager.AppSettings["ReportInterval"] ?? "10";

            string input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter server configuration (IP:Port:Interval):\n\n" +
                $"Example: 192.168.1.120:8091:10\n\n" +
                $"Current: {currentServer}:{currentPort}:{currentInterval}",
                "Configure Server",
                $"{currentServer}:{currentPort}:{currentInterval}");

            if (!string.IsNullOrEmpty(input))
            {
                string[] parts = input.Split(':');
                if (parts.Length == 3)
                {
                    if (int.TryParse(parts[1], out int port) && int.TryParse(parts[2], out int interval))
                    {
                        ConfigureServer(parts[0], port, interval);
                        ShowBalloonTip("Configuration Updated", "Server configuration saved.", ToolTipIcon.Info);
                    }
                    else
                    {
                        MessageBox.Show("Invalid port or interval. Please use format: IP:Port:Interval", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Invalid format. Please use format: IP:Port:Interval", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ConfigureServer(string serverIp, int port, int intervalMinutes)
        {
            try
            {
                _serverReporter?.SetServer(serverIp, port, intervalMinutes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error configuring server: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnToggleAutoStart(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem == null) return;

            if (StartupManager.ToggleStartup())
            {
                menuItem.Checked = StartupManager.IsStartupEnabled();
                string message = menuItem.Checked ? "Auto-start enabled" : "Auto-start disabled";
                ShowBalloonTip("Startup Configuration", message, ToolTipIcon.Info);
            }
            else
            {
                MessageBox.Show("Failed to update startup configuration.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Exit PatchinPal Client?\n\nThe server will not be able to manage this computer while the client is stopped.",
                "Confirm Exit",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Cleanup();
                Application.Exit();
            }
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            Cleanup();
        }

        private void Cleanup()
        {
            _httpServer?.Stop();
            _scheduleManager?.Stop();
            _serverReporter?.Stop();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }

        private void ShowBalloonTip(string title, string text, ToolTipIcon icon)
        {
            _trayIcon?.ShowBalloonTip(3000, title, text, icon);
        }
    }
}
