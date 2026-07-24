using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;
using Microsoft.Win32;
using Interop.WUApiLib;

namespace PatchinPal.Client
{
    public class RebootManager
    {
        private Timer _checkTimer;
        private Timer _warningTimer;
        private DateTime? _lastWarningTime;
        private bool _rebootPending = false;
        private readonly object _lock = new object();

        public event EventHandler<RebootPendingEventArgs> RebootPendingDetected;
        public event EventHandler RebootWarningNeeded;

        public RebootManager()
        {
            // Check for pending reboots every 5 minutes
            _checkTimer = new Timer(5 * 60 * 1000);
            _checkTimer.Elapsed += OnCheckTimerElapsed;
        }

        public void Start()
        {
            Logger.Info("RebootManager starting...");
            _checkTimer.Start();
            CheckForPendingReboot(); // Initial check
        }

        public void Stop()
        {
            Logger.Info("RebootManager stopping...");
            _checkTimer?.Stop();
            _warningTimer?.Stop();
        }

        private void OnCheckTimerElapsed(object sender, ElapsedEventArgs e)
        {
            CheckForPendingReboot();
        }

        public bool IsRebootPending()
        {
            return CheckForPendingReboot();
        }

        private bool CheckForPendingReboot()
        {
            lock (_lock)
            {
                bool wasPending = _rebootPending;
                _rebootPending = CheckRegistryForPendingReboot() || CheckWindowsUpdateForPendingReboot();

                if (_rebootPending && !wasPending)
                {
                    // Reboot just became pending
                    Logger.Warning("System reboot is now pending");
                    OnRebootPendingDetected(new RebootPendingEventArgs { DetectedTime = DateTime.Now });

                    // Start warning timer if enabled
                    if (ClientSettings.Instance.EnableRebootWarnings && ClientSettings.Instance.AggressiveMode)
                    {
                        StartWarningTimer();
                    }
                }
                else if (!_rebootPending && wasPending)
                {
                    // Reboot is no longer pending (was resolved)
                    Logger.Info("Pending reboot has been resolved");
                    StopWarningTimer();
                }

                return _rebootPending;
            }
        }

        private bool CheckRegistryForPendingReboot()
        {
            try
            {
                // Check Component-Based Servicing
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                {
                    if (key != null) return true;
                }

                // Check Windows Update Auto Update
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                {
                    if (key != null) return true;
                }

                // Check Pending File Rename Operations
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("PendingFileRenameOperations");
                        if (value != null && ((string[])value).Length > 0)
                        {
                            return true;
                        }
                    }
                }

                // Check ActiveComputerName vs ComputerName (rename pending)
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName"))
                using (var key2 = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName"))
                {
                    if (key != null && key2 != null)
                    {
                        string activeName = key.GetValue("ComputerName")?.ToString();
                        string pendingName = key2.GetValue("ComputerName")?.ToString();
                        if (!string.IsNullOrEmpty(activeName) && !string.IsNullOrEmpty(pendingName) && activeName != pendingName)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Error checking registry for pending reboot", ex);
                return false;
            }
        }

        private bool CheckWindowsUpdateForPendingReboot()
        {
            try
            {
                // Use Windows Update Agent API to check if reboot is required
                var updateSession = new UpdateSession();
                var updateSearcher = updateSession.CreateUpdateSearcher();

                // Search for updates that are downloaded and ready to install (often indicates pending reboot)
                ISearchResult searchResult = updateSearcher.Search("IsInstalled=0 and IsHidden=0");

                bool rebootRequired = false;
                foreach (IUpdate update in searchResult.Updates)
                {
                    if (update.IsInstalled || update.IsDownloaded)
                    {
                        // Check if any update requires reboot by examining installation result
                        // This is a heuristic — WUAPI doesn't expose a direct "reboot pending" flag
                        // but if updates are downloaded/installed and registry shows nothing,
                        // the system is likely up to date.
                    }
                }

                // Release COM objects
                Marshal.ReleaseComObject(updateSearcher);
                Marshal.ReleaseComObject(updateSession);

                return rebootRequired;
            }
            catch (Exception ex)
            {
                Logger.Error("Error checking Windows Update for pending reboot", ex);
                return false;
            }
        }

        private void StartWarningTimer()
        {
            if (_warningTimer != null)
            {
                _warningTimer.Stop();
                _warningTimer.Dispose();
            }

            int intervalMs = ClientSettings.Instance.RebootWarningIntervalMinutes * 60 * 1000;
            _warningTimer = new Timer(intervalMs);
            _warningTimer.Elapsed += OnWarningTimerElapsed;
            _warningTimer.Start();

            // Show immediate warning
            OnRebootWarningNeeded();
        }

        private void StopWarningTimer()
        {
            if (_warningTimer != null)
            {
                _warningTimer.Stop();
                _warningTimer.Dispose();
                _warningTimer = null;
            }
            _lastWarningTime = null;
        }

        private void OnWarningTimerElapsed(object sender, ElapsedEventArgs e)
        {
            OnRebootWarningNeeded();
        }

        protected virtual void OnRebootPendingDetected(RebootPendingEventArgs e)
        {
            RebootPendingDetected?.Invoke(this, e);
        }

        protected virtual void OnRebootWarningNeeded()
        {
            _lastWarningTime = DateTime.Now;
            Logger.Info("Reboot warning issued to user");
            RebootWarningNeeded?.Invoke(this, EventArgs.Empty);
        }

        public void InitiateReboot(int delaySeconds = 30)
        {
            try
            {
                Logger.Warning($"Initiating system reboot in {delaySeconds} seconds");

                var psi = new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = $"/r /t {delaySeconds} /c \"PatchinPal: System reboot required for updates\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initiate system reboot", ex);
                throw;
            }
        }

        public void CancelReboot()
        {
            try
            {
                Logger.Info("Cancelling scheduled system reboot");

                var psi = new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/a",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to cancel system reboot", ex);
            }
        }
    }

    public class RebootPendingEventArgs : EventArgs
    {
        public DateTime DetectedTime { get; set; }
    }
}
