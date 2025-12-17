using System;
using Microsoft.Win32;
using System.Reflection;

namespace PatchinPal.Server
{
    /// <summary>
    /// Manages Windows startup configuration for PatchinPal Server
    /// </summary>
    public static class ServerStartupManager
    {
        private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "PatchinPalServer";

        /// <summary>
        /// Check if server is configured to start with Windows
        /// </summary>
        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false))
                {
                    if (key == null) return false;
                    object value = key.GetValue(APP_NAME);
                    return value != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enable server to start with Windows
        /// </summary>
        public static bool EnableStartup()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (key == null) return false;
                    key.SetValue(APP_NAME, $"\"{exePath}\" /minimized");
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Disable server from starting with Windows
        /// </summary>
        public static bool DisableStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (key == null) return false;
                    key.DeleteValue(APP_NAME, false);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Toggle startup setting
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
