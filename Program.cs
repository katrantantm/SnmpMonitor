using System;
using SnmpMonitor.Config;
using SnmpMonitor.Logging;
using SnmpMonitor.Reporting;

namespace SnmpMonitor
{
    /// <summary>
    /// Точка входа приложения SNMP Poller
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var config = ConfigurationLoader.Load();
                
                var logger = new CompositeLogger(
                    new ConsoleLogger(config.Logging?.LogLevel ?? "Information"),
                    new FileLogger(config.Logging?.FilePath ?? "logs/poller.log", config.Logging?.LogLevel ?? "Information")
                );

                string serverIp = args.Length > 0 ? args[0] : config.Snmp?.TargetIp ?? "87.242.86.112";
                string community = config.Snmp?.Community ?? "public";
                string outputPath = config.Export?.OutputPath ?? "output";

                LogStartupInfo(logger, serverIp, community, outputPath);

                using var generator = new UniversalReportGenerator(serverIp, community, outputPath, logger);
                generator.GenerateAllReports();

                logger.Info("✅ Обработка завершена успешно");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Критическая ошибка: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void LogStartupInfo(ILogger logger, string serverIp, string community, string outputPath)
        {
            logger.Info("🚀 Запуск универсального SNMP Poller");
            logger.Info("Целевой сервер: {0}", serverIp);
            logger.Info("Сообщество: {0}", community);
            logger.Info("Путь вывода: {0}", outputPath);
        }
    }
}