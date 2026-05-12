using System;

namespace SnmpMonitor.Logging
{
    /// <summary>
    /// Композитный логгер, делегирующий вызовы нескольким дочерним логгерам
    /// </summary>
    public class CompositeLogger : ILogger
    {
        private readonly ILogger[] _loggers;

        public CompositeLogger(params ILogger[] loggers)
        {
            _loggers = loggers ?? Array.Empty<ILogger>();
        }

        public void Debug(string message, params object[] args) => 
            ExecuteOnAll(logger => logger.Debug(message, args));

        public void Info(string message, params object[] args) => 
            ExecuteOnAll(logger => logger.Info(message, args));

        public void Warn(string message, params object[] args) => 
            ExecuteOnAll(logger => logger.Warn(message, args));

        public void Error(string message, params object[] args) => 
            ExecuteOnAll(logger => logger.Error(message, args));

        public void Error(string message, Exception ex, params object[] args) => 
            ExecuteOnAll(logger => logger.Error(message, ex, args));

        private void ExecuteOnAll(Action<ILogger> action)
        {
            foreach (var logger in _loggers)
            {
                try
                {
                    action(logger);
                }
                catch
                {
                    // Игнорируем ошибки отдельных логгеров
                }
            }
        }
    }
}
