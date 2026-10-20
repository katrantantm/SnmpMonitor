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
        static int Main(string[] args)
        {
            try
            {
                var config = ConfigurationLoader.Load();

                var logger = new CompositeLogger(
                    new ConsoleLogger(config.Logging?.LogLevel ?? "Information"),
                    new FileLogger(config.Logging?.FilePath ?? "logs/poller.log", config.Logging?.LogLevel ?? "Information")
                );

                string serverIp = args.Length > 0 ? args[0] : config.Snmp?.TargetIp ?? "127.0.0.1";

                // Community берётся из конфигурации или переменной окружения SNMP_COMMUNITY
                // (см. ConfigurationLoader.ApplyEnvironmentOverrides). Секрет не логируется.
                string community = config.Snmp?.Community ?? "public";
                if (string.IsNullOrWhiteSpace(community))
                {
                    logger.Error("SNMP community не задан. Укажите его в appsettings.json " +
                                 "(или appsettings.local.json) либо через переменную окружения SNMP_COMMUNITY.");
                    return 1;
                }

                string outputPath = config.Export?.OutputPath ?? "output";

                LogStartupInfo(logger, serverIp, outputPath);

                using var generator = new UniversalReportGenerator(serverIp, community, outputPath, logger);
                generator.GenerateAllReports();

                logger.Info("✅ Обработка завершена успешно");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Критическая ошибка: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 2;
            }
        }

        private static void LogStartupInfo(ILogger logger, string serverIp, string outputPath)
        {
            logger.Info("🚀 Запуск универсального SNMP Poller");
            logger.Info("Целевой сервер: {0}", serverIp);
            logger.Info("Путь вывода: {0}", outputPath);
        }
    }
}
