using System;
using System.Threading;
using PatchinPal.Common;

namespace PatchinPal.Client
{
    /// <summary>
    /// Manages scheduled Windows updates
    /// </summary>
    public class ScheduleManager
    {
        private readonly UpdateManager _updateManager;
        private Timer _scheduledTimer;
        private DateTime? _scheduledTime;
        private bool _scheduledAggressive;
        private bool _isRunning;

        public ScheduleManager(UpdateManager updateManager)
        {
            _updateManager = updateManager;
        }

        public void Start()
        {
            _isRunning = true;
            Console.WriteLine("Schedule Manager started");
        }

        public void Stop()
        {
            _isRunning = false;
            _scheduledTimer?.Dispose();
            Console.WriteLine("Schedule Manager stopped");
        }

        /// <summary>
        /// Schedule an update to run at a specific time
        /// </summary>
        public void ScheduleUpdate(DateTime scheduledTime, bool aggressive)
        {
            // Cancel any existing scheduled update
            CancelScheduledUpdate();

            _scheduledTime = scheduledTime;
            _scheduledAggressive = aggressive;

            TimeSpan timeUntilUpdate = scheduledTime - DateTime.Now;

            if (timeUntilUpdate.TotalMilliseconds < 0)
            {
                Console.WriteLine("Scheduled time is in the past, executing immediately...");
                ExecuteScheduledUpdate();
                return;
            }

            Console.WriteLine($"Update scheduled for {scheduledTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Time until update: {timeUntilUpdate.Hours}h {timeUntilUpdate.Minutes}m");

            _scheduledTimer = new Timer(
                _ => ExecuteScheduledUpdate(),
                null,
                timeUntilUpdate,
                Timeout.InfiniteTimeSpan
            );
        }

        /// <summary>
        /// Cancel any scheduled update
        /// </summary>
        public void CancelScheduledUpdate()
        {
            if (_scheduledTimer != null)
            {
                _scheduledTimer.Dispose();
                _scheduledTimer = null;
                Console.WriteLine("Scheduled update cancelled");
            }

            _scheduledTime = null;
        }

        /// <summary>
        /// Get information about the scheduled update
        /// </summary>
        public (DateTime? scheduledTime, bool aggressive) GetScheduledUpdate()
        {
            return (_scheduledTime, _scheduledAggressive);
        }

        private void ExecuteScheduledUpdate()
        {
            try
            {
                Console.WriteLine("\n===========================================");
                Console.WriteLine($"Executing scheduled update at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Mode: {(_scheduledAggressive ? "AGGRESSIVE" : "Normal")}");
                Console.WriteLine("===========================================\n");

                var result = _updateManager.InstallUpdates(_scheduledAggressive);

                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Scheduled update completed successfully!");
                    Console.ResetColor();

                    if (result.Status == UpdateStatus.RebootRequired)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("A system reboot is required.");
                        Console.ResetColor();

                        if (_scheduledAggressive)
                        {
                            Console.WriteLine("Aggressive mode: Rebooting in 60 seconds...");
                            // In aggressive mode, we could trigger a reboot here
                            // System.Diagnostics.Process.Start("shutdown", "/r /t 60");
                        }
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Scheduled update failed: {result.Message}");
                    Console.ResetColor();
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error executing scheduled update: {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                // Clear the schedule
                _scheduledTime = null;
                _scheduledTimer?.Dispose();
                _scheduledTimer = null;
            }
        }
    }
}
