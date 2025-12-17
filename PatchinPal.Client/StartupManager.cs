using System;
using Microsoft.Win32;

namespace PatchinPal.Client
{
    /// <summary>
    /// Manages Windows startup configuration for PatchinPal Client
    /// </summary>
    public static class StartupManager
    {
        private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "PatchinPalClient";

        /// <summary>
        /// Check if auto-start is enabled
        /// </summary>
        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false))
                {
                    if (key == null) return false;
                    return key.GetValue(APP_NAME) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enable auto-start on Windows startup
        /// </summary>
        public static bool EnableStartup()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (key == null) return false;
                    key.SetValue(APP_NAME, $"\"{exePath}\" /background");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enabling startup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disable auto-start on Windows startup
        /// </summary>
        public static bool DisableStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (key == null) return false;

                    if (key.GetValue(APP_NAME) != null)
                    {
                        key.DeleteValue(APP_NAME);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disabling startup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Toggle auto-start setting
        /// </summary>
        public static bool ToggleStartup()
        {
            if (IsStartupEnabled())
            {
                return DisableStartup();
            }
            else
            {
                return EnableStartup();
            }
        }
    }
}
