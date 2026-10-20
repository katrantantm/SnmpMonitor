using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SnmpMonitor.Config
{
    public class AppConfig
    {
        public SnmpSettings? Snmp { get; set; }
        public ExcelSettings? Excel { get; set; }
        public LoggingSettings? Logging { get; set; }
        public ExportSettings? Export { get; set; }
    }

    public class SnmpSettings
    {
        public string TargetIp { get; set; } = "127.0.0.1";
        /// <summary>
        /// SNMP community. Не хранится в репозитории — задаётся через appsettings.local.json
        /// или переменную окружения SNMP_COMMUNITY (см. README).
        /// </summary>
        public string Community { get; set; } = "public";
        public int Timeout { get; set; } = 5000;
        public int Retries { get; set; } = 3;
        public string Version { get; set; } = "v2c";
        public int MaxProcesses { get; set; } = 200;
        public int MaxArpEntries { get; set; } = 1000;
    }

    public class ExcelSettings
    {
        public string? TemplatePath { get; set; }
        public bool AutoSave { get; set; } = true;
    }

    public class LoggingSettings
    {
        public string LogLevel { get; set; } = "Information";
        public string FilePath { get; set; } = "logs/poller.log";
    }

    public class ExportSettings
    {
        public string OutputPath { get; set; } = "output";
        public string[] Formats { get; set; } = new[] { "csv" };
    }

    public static class ConfigurationLoader
    {
        private static AppConfig? _config;
        private static readonly object _lockObj = new();

        public static AppConfig Load(string configPath = "appsettings.json")
        {
            lock (_lockObj)
            {
                if (_config != null) return _config;

                try
                {
                    var searchedPaths = new List<string>();
                    string? actualPath = null;

                    // 1. Относительно текущей директории
                    if (File.Exists(configPath))
                    {
                        actualPath = configPath;
                    }
                    else
                    {
                        // 2. Относительно директории исполняемого файла (важно при запуске из bin/publish)
                        searchedPaths.Add(configPath);
                        string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath);
                        if (File.Exists(exePath))
                        {
                            actualPath = exePath;
                        }
                        else
                        {
                            searchedPaths.Add(exePath);
                        }
                    }

                    if (actualPath == null)
                    {
                        Console.WriteLine($"⚠️ Файл конфигурации '{string.Join("', '", searchedPaths)}' не найден. Используются значения по умолчанию.");
                        _config = CreateDefaultConfig();
                    }
                    else
                    {
                        string json = File.ReadAllText(actualPath);
                        _config = JsonConvert.DeserializeObject<AppConfig>(json) ?? CreateDefaultConfig();
                        
                        // Создаем директорию для логов если не существует
                        if (!string.IsNullOrEmpty(_config.Logging?.FilePath))
                        {
                            string logDir = Path.GetDirectoryName(_config.Logging.FilePath);
                            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                            {
                                Directory.CreateDirectory(logDir);
                            }
                        }
                        
                        // Создаем директорию для экспорта если не существует
                        if (!string.IsNullOrEmpty(_config.Export?.OutputPath) && !Directory.Exists(_config.Export.OutputPath))
                        {
                            Directory.CreateDirectory(_config.Export.OutputPath);
                        }
                    }

                    ApplyEnvironmentOverrides(_config);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка загрузки конфигурации: {ex.Message}");
                    _config = CreateDefaultConfig();
                    ApplyEnvironmentOverrides(_config);
                }

                return _config;
            }
        }

        /// <summary>
        /// Переопределение чувствительных настроек через переменные окружения.
        /// Позволяет не хранить секреты (SNMP community) в репозитории.
        /// Поддерживаются: SNMP_TARGET_IP, SNMP_COMMUNITY, SNMP_TIMEOUT_MS
        /// </summary>
        private static void ApplyEnvironmentOverrides(AppConfig config)
        {
            if (config.Snmp == null)
                config.Snmp = new SnmpSettings();

            var targetIp = Environment.GetEnvironmentVariable("SNMP_TARGET_IP");
            if (!string.IsNullOrWhiteSpace(targetIp))
                config.Snmp.TargetIp = targetIp.Trim();

            var community = Environment.GetEnvironmentVariable("SNMP_COMMUNITY");
            if (!string.IsNullOrWhiteSpace(community))
                config.Snmp.Community = community.Trim();

            var timeout = Environment.GetEnvironmentVariable("SNMP_TIMEOUT_MS");
            if (!string.IsNullOrWhiteSpace(timeout) && int.TryParse(timeout, out int timeoutMs) && timeoutMs > 0)
                config.Snmp.Timeout = timeoutMs;
        }

        private static AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                Snmp = new SnmpSettings { TargetIp = "127.0.0.1", Community = "public", Timeout = 5000, Retries = 3 },
                Excel = new ExcelSettings { AutoSave = true },
                Logging = new LoggingSettings { LogLevel = "Information", FilePath = "logs/poller.log" },
                Export = new ExportSettings { OutputPath = "output", Formats = new[] { "csv" } }
            };
        }

        public static void Reset()
        {
            lock (_lockObj)
            {
                _config = null!;
            }
        }
    }

    /// <summary>
    /// Общие настройки SNMP (сообщество, лимиты)
    /// OID хранятся в Config/snmp-tables.json
    /// </summary>
    public static class SnmpConfig
    {
        static SnmpConfig()
        {
            Reload();
        }

        public static void Reload()
        {
            var config = ConfigurationLoader.Load();
            Community = config.Snmp?.Community ?? "public";
            MaxProcesses = config.Snmp?.MaxProcesses ?? 200;
            MaxArpEntries = config.Snmp?.MaxArpEntries ?? 1000;
        }

        public static string Community { get; private set; } = "public";
        public static int MaxProcesses { get; private set; } = 200;
        public static int MaxArpEntries { get; private set; } = 1000;
    }
}