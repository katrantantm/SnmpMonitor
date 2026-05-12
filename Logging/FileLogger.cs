using System;
using System.IO;

namespace SnmpMonitor.Logging
{
    /// <summary>
    /// Логгер с записью в файл
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _minLevel;
        private readonly string _filePath;
        private readonly object _lockObj = new();

        public FileLogger(string filePath, string minLevel = "Information")
        {
            _filePath = filePath;
            _minLevel = minLevel;

            EnsureDirectoryExists(filePath);
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private static int GetPriority(string level) => level switch
        {
            "Debug" => 0,
            "Information" => 1,
            "Warn" => 2,
            "Error" => 3,
            _ => 1
        };

        private void Write(string level, string message, Exception? ex, params object[] args)
        {
            if (GetPriority(level) < GetPriority(_minLevel)) return;

            string formatted = FormatMessage(message, args);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string prefix = $"[{timestamp}] [{level}] ";
            string fullMessage = prefix + formatted;

            if (ex != null)
            {
                fullMessage += $"\n{prefix}Exception: {ex.Message}";
                fullMessage += $"\n{prefix}Stack: {ex.StackTrace}";
            }

            lock (_lockObj)
            {
                try
                {
                    File.AppendAllText(_filePath, fullMessage + Environment.NewLine);
                }
                catch (Exception writeEx)
                {
                    Console.WriteLine($"[FileLogger] Ошибка записи в файл: {writeEx.Message}");
                }
            }
        }

        private static string FormatMessage(string message, object[] args) => 
            args.Length > 0 ? string.Format(message, args) : message;

        public void Debug(string message, params object?[] args) => 
            Write("Debug", message, null, args ?? Array.Empty<object>());

        public void Info(string message, params object?[] args) => 
            Write("Information", message, null, args ?? Array.Empty<object>());

        public void Warn(string message, params object?[] args) => 
            Write("Warn", message, null, args ?? Array.Empty<object>());

        public void Error(string message, params object?[] args) => 
            Write("Error", message, null, args ?? Array.Empty<object>());

        public void Error(string message, Exception ex, params object?[] args) => 
            Write("Error", message, ex, args ?? Array.Empty<object>());
    }
}
