using System;
using System.IO;
using Newtonsoft.Json;

namespace PatchinPal.Client
{
    public class ClientSettings
    {
        // Notification Settings
        public bool ShowNotifications { get; set; } = true;
        public bool ShowCheckNotifications { get; set; } = true;
        public bool ShowInstallNotifications { get; set; } = true;
        public bool ShowErrorNotifications { get; set; } = true;
        public bool ShowSuccessNotifications { get; set; } = true;

        // Logging Settings
        public bool EnableLogging { get; set; } = true;
        public string LogLevel { get; set; } = "Info"; // Debug, Info, Warning, Error

        // Update Settings
        public bool AutoInstallUpdates { get; set; } = false;
        public int CheckIntervalMinutes { get; set; } = 60;

        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PatchinPal", "Client");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");
        private static ClientSettings _instance;
        private static readonly object _lock = new object();

        public static ClientSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = Load();
                        }
                    }
                }
                return _instance;
            }
        }

        private static ClientSettings Load()
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonConvert.DeserializeObject<ClientSettings>(json);
                    Logger.Info("Settings loaded successfully");
                    return settings ?? new ClientSettings();
                }
                else
                {
                    Logger.Info("No settings file found, using defaults");
                    var defaultSettings = new ClientSettings();
                    defaultSettings.Save();
                    return defaultSettings;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load settings, using defaults", ex);
                return new ClientSettings();
            }
        }

        public void Save()
        {
            try
            {
                lock (_lock)
                {
                    string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                    File.WriteAllText(SettingsFilePath, json);
                    Logger.Info("Settings saved successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save settings", ex);
            }
        }

        public void ApplyLogLevel()
        {
            try
            {
                Logger.IsEnabled = EnableLogging;

                switch (LogLevel.ToLower())
                {
                    case "debug":
                        Logger.MinimumLevel = PatchinPal.Client.LogLevel.Debug;
                        break;
                    case "info":
                        Logger.MinimumLevel = PatchinPal.Client.LogLevel.Info;
                        break;
                    case "warning":
                        Logger.MinimumLevel = PatchinPal.Client.LogLevel.Warning;
                        break;
                    case "error":
                        Logger.MinimumLevel = PatchinPal.Client.LogLevel.Error;
                        break;
                    default:
                        Logger.MinimumLevel = PatchinPal.Client.LogLevel.Info;
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply log level", ex);
            }
        }
    }
}
