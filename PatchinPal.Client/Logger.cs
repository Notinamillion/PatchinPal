using System;
using System.IO;

namespace PatchinPal.Client
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public static class Logger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PatchinPal", "Client", "logs");

        private static readonly string LogFilePath = Path.Combine(LogDirectory, $"patchinpal-{DateTime.Now:yyyy-MM-dd}.log");
        private static readonly object _lock = new object();
        private static bool _isEnabled = true;
        private static LogLevel _minimumLevel = LogLevel.Info;

        static Logger()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }
            catch
            {
                // If we can't create the log directory, disable logging
                _isEnabled = false;
            }
        }

        public static bool IsEnabled
        {
            get { return _isEnabled; }
            set { _isEnabled = value; }
        }

        public static LogLevel MinimumLevel
        {
            get { return _minimumLevel; }
            set { _minimumLevel = value; }
        }

        public static void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        public static void Info(string message)
        {
            Log(LogLevel.Info, message);
        }

        public static void Warning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        public static void Error(string message, Exception ex = null)
        {
            if (ex != null)
            {
                message = $"{message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            Log(LogLevel.Error, message);
        }

        private static void Log(LogLevel level, string message)
        {
            if (!_isEnabled || level < _minimumLevel)
                return;

            try
            {
                lock (_lock)
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level.ToString().ToUpper()}] {message}";

                    // Write to file
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);

                    // Also write to console if available
                    if (Console.OpenStandardOutput() != Stream.Null)
                    {
                        ConsoleColor originalColor = Console.ForegroundColor;
                        Console.ForegroundColor = GetColorForLevel(level);
                        Console.WriteLine(logEntry);
                        Console.ForegroundColor = originalColor;
                    }
                }
            }
            catch
            {
                // Silently fail if we can't write to the log
            }
        }

        private static ConsoleColor GetColorForLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug: return ConsoleColor.Gray;
                case LogLevel.Info: return ConsoleColor.White;
                case LogLevel.Warning: return ConsoleColor.Yellow;
                case LogLevel.Error: return ConsoleColor.Red;
                default: return ConsoleColor.White;
            }
        }

        public static void CleanupOldLogs(int daysToKeep = 30)
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                foreach (var file in Directory.GetFiles(LogDirectory, "*.log"))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        File.Delete(file);
                        Info($"Deleted old log file: {fileInfo.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Error("Failed to cleanup old logs", ex);
            }
        }
    }
}
