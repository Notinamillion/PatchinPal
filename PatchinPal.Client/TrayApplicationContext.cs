using System;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Timers;
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
        private RebootManager _rebootManager;
        private System.Timers.Timer _updateCheckTimer;
        private bool _isFirstRun;

        public TrayApplicationContext()
        {
            Application.ApplicationExit += OnApplicationExit;

            // Initialize settings and logging
            ClientSettings.Instance.ApplyLogLevel();
            Logger.Info("PatchinPal Client starting...");
            Logger.Info($"Logging enabled: {Logger.IsEnabled}, Level: {Logger.MinimumLevel}");

            // Cleanup old logs
            Logger.CleanupOldLogs(30);

            // Check if this is first run
            _isFirstRun = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ServerAddress"]);

            // Initialize components
            InitializeComponents();

            // Create tray icon
            CreateTrayIcon();

            // Handle first run
            if (_isFirstRun)
            {
                Logger.Info("First run detected");
                HandleFirstRun();
            }
            else
            {
                // Normal startup
                Logger.Info("Client started successfully");
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
                _rebootManager = new RebootManager();

                // Subscribe to reboot events
                _rebootManager.RebootPendingDetected += OnRebootPendingDetected;
                _rebootManager.RebootWarningNeeded += OnRebootWarningNeeded;

                _httpServer.Start();
                _scheduleManager.Start();
                _serverReporter.Start();
                _rebootManager.Start();

                // Start automatic update checking if AutoInstallUpdates or AggressiveMode is enabled
                if (ClientSettings.Instance.AutoInstallUpdates || ClientSettings.Instance.AggressiveMode)
                {
                    StartAutomaticUpdateChecking();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error initializing PatchinPal Client", ex);
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
            contextMenu.Items.Add("View Update History", null, OnViewHistory);
            contextMenu.Items.Add("Configure Server...", null, OnConfigureServer);
            contextMenu.Items.Add(new ToolStripSeparator());

            var autoStartItem = new ToolStripMenuItem("Run at Startup");
            autoStartItem.Checked = StartupManager.IsStartupEnabled();
            autoStartItem.Click += OnToggleAutoStart;
            contextMenu.Items.Add(autoStartItem);

            var notificationsItem = new ToolStripMenuItem("Show Notifications");
            notificationsItem.Checked = ClientSettings.Instance.ShowNotifications;
            notificationsItem.Click += OnToggleNotifications;
            contextMenu.Items.Add(notificationsItem);

            var aggressiveModeItem = new ToolStripMenuItem("Aggressive Mode");
            aggressiveModeItem.Checked = ClientSettings.Instance.AggressiveMode;
            aggressiveModeItem.Click += OnToggleAggressiveMode;
            contextMenu.Items.Add(aggressiveModeItem);

            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Reboot Now", null, OnRebootNow);
            contextMenu.Items.Add("Settings...", null, OnOpenSettings);

            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, OnExit);

            // Create tray icon with custom logo
            Icon customIcon = null;
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
                if (File.Exists(iconPath))
                {
                    customIcon = new Icon(iconPath);
                    Logger.Info("Loaded custom tray icon");
                }
                else
                {
                    Logger.Warning($"Custom icon not found at {iconPath}, using default");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load custom icon, using default", ex);
            }

            _trayIcon = new NotifyIcon
            {
                Icon = customIcon ?? SystemIcons.Application,
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
                Logger.Info("User initiated update check");
                ShowBalloonTip("Checking for Updates", "Searching for available updates...", ToolTipIcon.Info, ClientSettings.Instance.ShowCheckNotifications);

                var updates = _updateManager.CheckForUpdates();

                UpdateHistory.RecordCheck(true, updates.Count, $"Found {updates.Count} update(s)");

                if (updates.Count == 0)
                {
                    Logger.Info("No updates available");
                    ShowBalloonTip("No Updates", "Your system is up to date.", ToolTipIcon.Info, ClientSettings.Instance.ShowSuccessNotifications);
                }
                else
                {
                    Logger.Info($"Found {updates.Count} available updates");
                    ShowBalloonTip("Updates Available", $"{updates.Count} update(s) available. Right-click to install.", ToolTipIcon.Warning, ClientSettings.Instance.ShowCheckNotifications);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to check for updates", ex);
                UpdateHistory.RecordCheck(false, 0, $"Error: {ex.Message}");
                ShowBalloonTip("Check Failed", $"Error: {ex.Message}", ToolTipIcon.Error, ClientSettings.Instance.ShowErrorNotifications);
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
                    Logger.Info("User initiated update installation");
                    ShowBalloonTip("Installing Updates", "Update installation in progress...", ToolTipIcon.Info, ClientSettings.Instance.ShowInstallNotifications);

                    var updates = _updateManager.CheckForUpdates();
                    var response = _updateManager.InstallUpdates(false);

                    if (response.Success)
                    {
                        Logger.Info($"Updates installed successfully, RebootRequired={response.Status == UpdateStatus.RebootRequired}");
                        UpdateHistory.RecordInstall(true, updates.Count, response.Status == UpdateStatus.RebootRequired, response.Message, updates);

                        if (response.Status == UpdateStatus.RebootRequired)
                        {
                            ShowBalloonTip("Updates Installed", "Updates installed successfully. Reboot required.", ToolTipIcon.Info, ClientSettings.Instance.ShowSuccessNotifications);
                        }
                        else
                        {
                            ShowBalloonTip("Updates Installed", "Updates installed successfully.", ToolTipIcon.Info, ClientSettings.Instance.ShowSuccessNotifications);
                        }
                    }
                    else
                    {
                        Logger.Error($"Update installation failed: {response.Message}");
                        UpdateHistory.RecordInstall(false, 0, false, response.Message);
                        ShowBalloonTip("Installation Failed", response.Message, ToolTipIcon.Error, ClientSettings.Instance.ShowErrorNotifications);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Error installing updates", ex);
                    UpdateHistory.RecordInstall(false, 0, false, $"Exception: {ex.Message}");
                    ShowBalloonTip("Installation Error", ex.Message, ToolTipIcon.Error, ClientSettings.Instance.ShowErrorNotifications);
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
            _rebootManager?.Stop();
            _updateCheckTimer?.Stop();
            _updateCheckTimer?.Dispose();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }

        private void ShowBalloonTip(string title, string text, ToolTipIcon icon, bool forceShow = true)
        {
            if (ClientSettings.Instance.ShowNotifications || forceShow)
            {
                _trayIcon?.ShowBalloonTip(3000, title, text, icon);
            }
        }

        private void OnViewHistory(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("User viewing update history");
                string history = UpdateHistory.GetHistorySummary();
                MessageBox.Show(history, "Update History", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to view history", ex);
                MessageBox.Show($"Error viewing history: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnToggleNotifications(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem == null) return;

            ClientSettings.Instance.ShowNotifications = !ClientSettings.Instance.ShowNotifications;
            ClientSettings.Instance.Save();
            menuItem.Checked = ClientSettings.Instance.ShowNotifications;

            string message = ClientSettings.Instance.ShowNotifications ? "Notifications enabled" : "Notifications disabled";
            Logger.Info(message);
            ShowBalloonTip("Notification Settings", message, ToolTipIcon.Info, true);
        }

        private void OnOpenSettings(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("User opening settings");

                string rebootStatus = _rebootManager?.IsRebootPending() == true ? "YES - Reboot Required!" : "No";

                string message = "Settings:\n\n" +
                    $"Notifications: {(ClientSettings.Instance.ShowNotifications ? "Enabled" : "Disabled")}\n" +
                    $"  - Check Notifications: {(ClientSettings.Instance.ShowCheckNotifications ? "On" : "Off")}\n" +
                    $"  - Install Notifications: {(ClientSettings.Instance.ShowInstallNotifications ? "On" : "Off")}\n" +
                    $"  - Success Notifications: {(ClientSettings.Instance.ShowSuccessNotifications ? "On" : "Off")}\n" +
                    $"  - Error Notifications: {(ClientSettings.Instance.ShowErrorNotifications ? "On" : "Off")}\n\n" +
                    $"Logging: {(ClientSettings.Instance.EnableLogging ? "Enabled" : "Disabled")}\n" +
                    $"Log Level: {ClientSettings.Instance.LogLevel}\n\n" +
                    $"Aggressive Mode: {(ClientSettings.Instance.AggressiveMode ? "Enabled" : "Disabled")}\n" +
                    $"Reboot Warnings: {(ClientSettings.Instance.EnableRebootWarnings ? "Enabled" : "Disabled")}\n" +
                    $"Warning Interval: Every {ClientSettings.Instance.RebootWarningIntervalMinutes} minutes\n\n" +
                    $"Reboot Pending: {rebootStatus}\n\n" +
                    "Use the tray menu to toggle settings.\n" +
                    $"Settings file: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PatchinPal", "Client", "settings.json")}";

                MessageBox.Show(message, "PatchinPal Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open settings", ex);
                MessageBox.Show($"Error opening settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnToggleAggressiveMode(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem == null) return;

            ClientSettings.Instance.AggressiveMode = !ClientSettings.Instance.AggressiveMode;
            ClientSettings.Instance.Save();
            menuItem.Checked = ClientSettings.Instance.AggressiveMode;

            string message = ClientSettings.Instance.AggressiveMode
                ? "Aggressive Mode enabled - Updates will be installed automatically and reboot warnings will be shown"
                : "Aggressive Mode disabled";

            Logger.Info(message);
            ShowBalloonTip("Aggressive Mode", message, ToolTipIcon.Info, true);

            // If aggressive mode was just enabled and reboot is pending, start warnings
            if (ClientSettings.Instance.AggressiveMode && _rebootManager?.IsRebootPending() == true)
            {
                ShowBalloonTip("Reboot Required", "System restart is required to complete updates. Click 'Reboot Now' in the menu when ready.", ToolTipIcon.Warning, true);
            }
        }

        private void OnRebootNow(object sender, EventArgs e)
        {
            try
            {
                bool rebootPending = _rebootManager?.IsRebootPending() ?? false;
                string message = rebootPending
                    ? "A system restart is required to complete updates.\n\nReboot now?"
                    : "Are you sure you want to restart your computer now?";

                var result = MessageBox.Show(message, "Confirm Reboot", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    Logger.Warning("User initiated system reboot");
                    _rebootManager?.InitiateReboot(30);
                    ShowBalloonTip("Rebooting", "System will restart in 30 seconds...", ToolTipIcon.Warning, true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initiate reboot", ex);
                MessageBox.Show($"Error initiating reboot: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnRebootPendingDetected(object sender, RebootPendingEventArgs e)
        {
            Logger.Warning($"Reboot pending detected at {e.DetectedTime}");

            if (ClientSettings.Instance.AggressiveMode && ClientSettings.Instance.EnableRebootWarnings)
            {
                ShowBalloonTip("Reboot Required",
                    "Windows updates require a system restart. Click 'Reboot Now' when convenient.",
                    ToolTipIcon.Warning, true);
            }
        }

        private void OnRebootWarningNeeded(object sender, EventArgs e)
        {
            if (ClientSettings.Instance.AggressiveMode && ClientSettings.Instance.EnableRebootWarnings)
            {
                ShowBalloonTip("Reboot Still Required",
                    "Your system still needs to restart to complete updates. Please reboot soon.",
                    ToolTipIcon.Warning, true);
            }
        }

        private void StartAutomaticUpdateChecking()
        {
            if (_updateCheckTimer != null)
            {
                _updateCheckTimer.Stop();
                _updateCheckTimer.Dispose();
            }

            int intervalMs = ClientSettings.Instance.CheckIntervalMinutes * 60 * 1000;
            _updateCheckTimer = new System.Timers.Timer(intervalMs);
            _updateCheckTimer.Elapsed += OnAutomaticUpdateCheck;
            _updateCheckTimer.AutoReset = true;
            _updateCheckTimer.Start();

            Logger.Info($"Automatic update checking started - interval: {ClientSettings.Instance.CheckIntervalMinutes} minutes");

            // Do an immediate check on startup
            System.Threading.Tasks.Task.Run(() =>
            {
                System.Threading.Thread.Sleep(5000); // Wait 5 seconds after startup
                OnAutomaticUpdateCheck(null, null);
            });
        }

        private void OnAutomaticUpdateCheck(object sender, ElapsedEventArgs e)
        {
            try
            {
                Logger.Info("Automatic update check started");
                var updates = _updateManager.CheckForUpdates();

                UpdateHistory.RecordCheck(true, updates.Count, $"Found {updates.Count} update(s)");

                if (updates.Count > 0)
                {
                    Logger.Info($"Found {updates.Count} available updates in automatic check");

                    if (ClientSettings.Instance.AggressiveMode && ClientSettings.Instance.AutoInstallUpdates)
                    {
                        // Auto-install if aggressive mode is on
                        Logger.Info("Aggressive mode enabled - auto-installing updates");
                        ShowBalloonTip("Installing Updates",
                            $"Automatically installing {updates.Count} update(s)...",
                            ToolTipIcon.Info,
                            ClientSettings.Instance.ShowInstallNotifications);

                        var result = _updateManager.InstallUpdates(true);

                        if (result.Success)
                        {
                            Logger.Info($"Auto-install completed successfully, RebootRequired={result.Status == UpdateStatus.RebootRequired}");
                            UpdateHistory.RecordInstall(true, updates.Count, result.Status == UpdateStatus.RebootRequired, result.Message, updates);

                            if (result.Status == UpdateStatus.RebootRequired)
                            {
                                ShowBalloonTip("Updates Installed - Reboot Required",
                                    "Updates were installed automatically. System restart required.",
                                    ToolTipIcon.Warning, true);
                            }
                            else
                            {
                                ShowBalloonTip("Updates Installed",
                                    $"{updates.Count} update(s) installed successfully.",
                                    ToolTipIcon.Info,
                                    ClientSettings.Instance.ShowSuccessNotifications);
                            }
                        }
                        else
                        {
                            Logger.Error($"Auto-install failed: {result.Message}");
                            UpdateHistory.RecordInstall(false, 0, false, result.Message);
                            ShowBalloonTip("Auto-Install Failed",
                                result.Message,
                                ToolTipIcon.Error,
                                ClientSettings.Instance.ShowErrorNotifications);
                        }
                    }
                    else
                    {
                        // Just notify user
                        ShowBalloonTip("Updates Available",
                            $"{updates.Count} update(s) available. Right-click tray icon to install.",
                            ToolTipIcon.Info,
                            ClientSettings.Instance.ShowCheckNotifications);
                    }
                }
                else
                {
                    Logger.Info("System is up to date (automatic check)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Automatic update check failed", ex);
                UpdateHistory.RecordCheck(false, 0, $"Error: {ex.Message}");
            }
        }
    }
}
