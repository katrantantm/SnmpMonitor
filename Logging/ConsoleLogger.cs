using System;

namespace SnmpMonitor.Logging
{
    /// <summary>
    /// Логгер с выводом в консоль с цветовой дифференциацией уровней
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly string _minLevel;
        private readonly object _lockObj = new();

        public ConsoleLogger(string minLevel = "Information")
        {
            _minLevel = minLevel;
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
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string prefix = $"[{timestamp}] [{level}] ";
            string fullMessage = prefix + formatted;

            if (ex != null)
            {
                fullMessage += $"\n{prefix}Exception: {ex.Message}";
                fullMessage += $"\n{prefix}Stack: {ex.StackTrace}";
            }

            lock (_lockObj)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = GetLogLevelColor(level);
                Console.WriteLine(fullMessage);
                Console.ForegroundColor = originalColor;
            }
        }

        private static ConsoleColor GetLogLevelColor(string level) => level switch
        {
            "Error" => ConsoleColor.Red,
            "Warn" => ConsoleColor.Yellow,
            "Debug" => ConsoleColor.Gray,
            _ => Console.ForegroundColor
        };

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