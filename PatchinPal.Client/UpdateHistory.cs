using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PatchinPal.Common;

namespace PatchinPal.Client
{
    public class UpdateHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } // "Check", "Install", "Schedule"
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<WindowsUpdate> Updates { get; set; }
        public bool RebootRequired { get; set; }
        public int UpdatesInstalled { get; set; }

        public UpdateHistoryEntry()
        {
            Timestamp = DateTime.Now;
            Updates = new List<WindowsUpdate>();
        }
    }

    public static class UpdateHistory
    {
        private static readonly string HistoryDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PatchinPal", "Client");

        private static readonly string HistoryFilePath = Path.Combine(HistoryDirectory, "update-history.json");
        private static readonly object _lock = new object();
        private static List<UpdateHistoryEntry> _history;

        static UpdateHistory()
        {
            try
            {
                if (!Directory.Exists(HistoryDirectory))
                {
                    Directory.CreateDirectory(HistoryDirectory);
                }
                LoadHistory();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize update history", ex);
                _history = new List<UpdateHistoryEntry>();
            }
        }

        private static void LoadHistory()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(HistoryFilePath))
                    {
                        string json = File.ReadAllText(HistoryFilePath);
                        _history = JsonConvert.DeserializeObject<List<UpdateHistoryEntry>>(json);
                        if (_history == null)
                            _history = new List<UpdateHistoryEntry>();

                        Logger.Debug($"Loaded {_history.Count} history entries");
                    }
                    else
                    {
                        _history = new List<UpdateHistoryEntry>();
                        Logger.Debug("No history file found, starting fresh");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to load history, starting fresh", ex);
                    _history = new List<UpdateHistoryEntry>();
                }
            }
        }

        private static void SaveHistory()
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(_history, Formatting.Indented);
                    File.WriteAllText(HistoryFilePath, json);
                    Logger.Debug($"Saved {_history.Count} history entries");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to save history", ex);
                }
            }
        }

        public static void RecordCheck(bool success, int updatesFound, string message = "")
        {
            var entry = new UpdateHistoryEntry
            {
                Action = "Check",
                Success = success,
                Message = message,
                UpdatesInstalled = updatesFound
            };

            AddEntry(entry);
            Logger.Info($"Recorded check: {updatesFound} updates found, Success={success}");
        }

        public static void RecordInstall(bool success, int updatesInstalled, bool rebootRequired, string message = "", List<WindowsUpdate> updates = null)
        {
            var entry = new UpdateHistoryEntry
            {
                Action = "Install",
                Success = success,
                Message = message,
                UpdatesInstalled = updatesInstalled,
                RebootRequired = rebootRequired,
                Updates = updates ?? new List<WindowsUpdate>()
            };

            AddEntry(entry);
            Logger.Info($"Recorded install: {updatesInstalled} updates installed, Success={success}, RebootRequired={rebootRequired}");
        }

        public static void RecordSchedule(DateTime scheduledTime, bool aggressive, string message = "")
        {
            var entry = new UpdateHistoryEntry
            {
                Action = "Schedule",
                Success = true,
                Message = $"Scheduled for {scheduledTime:yyyy-MM-dd HH:mm} (Aggressive={aggressive}). {message}"
            };

            AddEntry(entry);
            Logger.Info($"Recorded schedule: {scheduledTime}, Aggressive={aggressive}");
        }

        private static void AddEntry(UpdateHistoryEntry entry)
        {
            lock (_lock)
            {
                _history.Add(entry);

                // Keep only last 1000 entries
                if (_history.Count > 1000)
                {
                    _history = _history.OrderByDescending(e => e.Timestamp).Take(1000).ToList();
                }

                SaveHistory();
            }
        }

        public static List<UpdateHistoryEntry> GetRecentHistory(int count = 50)
        {
            lock (_lock)
            {
                return _history.OrderByDescending(e => e.Timestamp).Take(count).ToList();
            }
        }

        public static List<UpdateHistoryEntry> GetHistorySince(DateTime since)
        {
            lock (_lock)
            {
                return _history.Where(e => e.Timestamp >= since).OrderByDescending(e => e.Timestamp).ToList();
            }
        }

        public static string GetHistorySummary()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Update History Summary ===");
                sb.AppendLine($"Total Entries: {_history.Count}");

                var last30Days = _history.Where(e => e.Timestamp >= DateTime.Now.AddDays(-30)).ToList();
                sb.AppendLine($"\nLast 30 Days:");
                sb.AppendLine($"  Checks: {last30Days.Count(e => e.Action == "Check")}");
                sb.AppendLine($"  Installs: {last30Days.Count(e => e.Action == "Install")}");
                sb.AppendLine($"  Total Updates Installed: {last30Days.Where(e => e.Action == "Install").Sum(e => e.UpdatesInstalled)}");
                sb.AppendLine($"  Successful: {last30Days.Count(e => e.Success)}");
                sb.AppendLine($"  Failed: {last30Days.Count(e => !e.Success)}");

                var recent = GetRecentHistory(10);
                sb.AppendLine($"\nRecent Activity (Last 10):");
                foreach (var entry in recent)
                {
                    string status = entry.Success ? "✓" : "✗";
                    sb.AppendLine($"  [{entry.Timestamp:yyyy-MM-dd HH:mm}] {status} {entry.Action} - {entry.Message}");
                }

                return sb.ToString();
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _history.Clear();
                SaveHistory();
                Logger.Info("History cleared");
            }
        }
    }
}
