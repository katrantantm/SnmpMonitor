using System;
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
        public string TargetIp { get; set; } = "87.242.86.112";
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
                    if (!File.Exists(configPath))
                    {
                        Console.WriteLine($"⚠️ Файл конфигурации '{configPath}' не найден. Используются значения по умолчанию.");
                        _config = CreateDefaultConfig();
                    }
                    else
                    {
                        string json = File.ReadAllText(configPath);
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка загрузки конфигурации: {ex.Message}");
                    _config = CreateDefaultConfig();
                }

                return _config;
            }
        }

        private static AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                Snmp = new SnmpSettings { TargetIp = "87.242.86.112", Community = "public", Timeout = 5000, Retries = 3 },
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