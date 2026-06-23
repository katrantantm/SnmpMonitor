using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SnmpMonitor.Config;
using SnmpMonitor.Logging;
using SnmpMonitor.Reporting;
using SnmpMonitor.Services;

namespace SnmpMonitor
{
    /// <summary>
    /// Точка входа приложения SNMP Poller с DI контейнером
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var builder = Host.CreateApplicationBuilder(args);

                // Загрузка конфигурации
                var config = ConfigurationLoader.Load();

                // Регистрация сервисов
                // Регистрируем AppConfig напрямую для доступа в других сервисах
                builder.Services.AddSingleton<AppConfig>(config);

                builder.Services.AddSnmpServices(
                    config.Snmp?.TargetIp ?? "87.242.86.112",
                    config.Snmp?.Community ?? "public",
                    config.Snmp?.Timeout ?? 5000);

                builder.Services.AddTransient<UniversalReportGenerator>();
                builder.Services.AddTransient<UniversalCsvReporter>();
                builder.Services.AddTransient<UniversalPdfReporter>();

                var host = builder.Build();

                // Получение параметров из командной строки
                string serverIp = args.Length > 0 ? args[0] : config.Snmp?.TargetIp ?? "87.242.86.112";
                string community = config.Snmp?.Community ?? "public";
                string outputPath = config.Export?.OutputPath ?? "output";

                // Логирование информации о запуске
                var logger = host.Services.GetRequiredService<ILogger>();
                LogStartupInfo(logger, serverIp, community, outputPath);

                // Получение генератора отчетов из DI контейнера
                using var generator = host.Services.GetRequiredService<UniversalReportGenerator>();
                await Task.Run(() => generator.GenerateAllReports());

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
