using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;
using Microsoft.Win32;

namespace PatchinPal.Client
{
    public class RebootManager
    {
        private Timer _checkTimer;
        private Timer _warningTimer;
        private DateTime? _lastWarningTime;
        private bool _rebootPending = false;

        public event EventHandler<RebootPendingEventArgs> RebootPendingDetected;
        public event EventHandler RebootWarningNeeded;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetSystemRegistryQuota(out uint pdwQuotaAllowed, out uint pdwQuotaUsed);

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
                // Use WMI to check for pending reboot from Windows Update
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT RebootRequired FROM Win32_QuickFixEngineering"))
                {
                    foreach (var item in searcher.Get())
                    {
                        if (item["RebootRequired"] != null && (bool)item["RebootRequired"])
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch
            {
                // WMI query failed, fall back to registry check only
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
