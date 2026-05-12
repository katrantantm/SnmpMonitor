using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnmpMonitor.Config;
using SnmpMonitor.Logging;

namespace SnmpMonitor.Reporting
{
    /// <summary>
    /// Универсальный генератор CSV отчетов на основе конфигурации OID
    /// </summary>
    public class UniversalCsvReporter : IDisposable
    {
        private readonly string _outputPath;
        private readonly ILogger _logger;
        private bool _disposed;
        private readonly OidConfiguration _config;

        public UniversalCsvReporter(string outputPath, ILogger? logger = null, string? configPath = null)
        {
            _outputPath = outputPath;
            _logger = logger ?? new ConsoleLogger();
            _config = OidConfigLoader.Load(configPath ?? "Config/snmp-tables.json");
            
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
        }

        /// <summary>
        /// Экспорт скалярных значений в CSV
        /// </summary>
        public void ExportScalars(string category, Dictionary<string, string> values)
        {
            if (!_config.Scalars.ContainsKey(category) || values == null || values.Count == 0) return;

            string fileName = $"{category}_scalars.csv";
            string filePath = Path.Combine(_outputPath, fileName);
            _logger.Info("Запись CSV: {0}", filePath);

            var headers = new[] { "Parameter", "Value" };
            var rows = new List<string[]>();

            foreach (var kvp in values)
            {
                rows.Add(new[]
                {
                    EscapeCsv(kvp.Key),
                    EscapeCsv(kvp.Value)
                });
            }

            WriteCsvFile(filePath, headers, rows);
        }

        /// <summary>
        /// Универсальный экспорт таблицы на основе конфигурации
        /// </summary>
        public void ExportTable(string tableKey, Dictionary<string, Dictionary<string, string>> data)
        {
            if (!_config.Tables.ContainsKey(tableKey) || data == null || data.Count == 0) return;

            var tableConfig = _config.Tables[tableKey];
            string fileName = $"{tableKey}.csv";
            string filePath = Path.Combine(_outputPath, fileName);
            _logger.Info("Запись CSV: {0}", filePath);

            // Заголовки из конфигурации полей
            var headers = tableConfig.Fields.Select(f => f.Name).ToArray();
            var rows = new List<string[]>();

            // Данные: каждая строка - значения полей для одного индекса
            foreach (var entry in data.Values)
            {
                var row = new string[headers.Length];
                for (int i = 0; i < headers.Length; i++)
                {
                    string fieldName = headers[i];
                    string rawValue = entry.ContainsKey(fieldName) ? entry[fieldName] : "";
                    
                    // Применяем форматирование из конфигурации
                    var fieldConfig = tableConfig.Fields.FirstOrDefault(f => f.Name == fieldName);
                    row[i] = FormatValue(rawValue, fieldConfig);
                }
                rows.Add(row);
            }

            WriteCsvFile(filePath, headers, rows);
        }

        /// <summary>
        /// Форматирование значения согласно конфигурации поля
        /// </summary>
        private string FormatValue(string rawValue, FieldConfig? fieldConfig)
        {
            if (string.IsNullOrEmpty(rawValue)) return "";
            
            if (fieldConfig == null) return EscapeCsv(rawValue);
            
            // Применяем маппинг статусов
            if (fieldConfig.Map != null && fieldConfig.Map.TryGetValue(rawValue, out var mappedValue))
            {
                return EscapeCsv(mappedValue);
            }
            
            // Форматируем скорость (ifHighSpeed возвращается в Мбит/с)
            if (fieldConfig.Format == "speed_mbps" && ulong.TryParse(rawValue, out ulong speedMbps))
            {
                if (speedMbps >= 1_000)
                    return EscapeCsv($"{(speedMbps / 1_000.0):F1} Gbps");
                return EscapeCsv($"{speedMbps} Mbps");
            }
            
            // Если значение не числовое (например, OID или строка), возвращаем N/A
            if (fieldConfig.Format == "speed_mbps")
            {
                return EscapeCsv("N/A");
            }
            
            return EscapeCsv(rawValue);
        }

        /// <summary>
        /// Экспорт всех таблиц из конфигурации
        /// </summary>
        public void ExportAllTables(Func<string, Dictionary<string, Dictionary<string, string>>> tableFetcher)
        {
            foreach (var tableKey in _config.Tables.Keys)
            {
                try
                {
                    var data = tableFetcher(tableKey);
                    if (data != null && data.Count > 0)
                    {
                        ExportTable(tableKey, data);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("Ошибка экспорта таблицы {0}: {1}", tableKey, ex.Message);
                }
            }
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private void WriteCsvFile(string filePath, string[] headers, List<string[]> rows)
        {
            using (var writer = new StreamWriter(filePath, false))
            {
                // Заголовки
                writer.WriteLine(string.Join(",", headers));
                
                // Данные
                foreach (var row in rows)
                {
                    writer.WriteLine(string.Join(",", row));
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _logger.Debug("Universal CSV Reporter освобожден");
            _disposed = true;
        }

        ~UniversalCsvReporter() { Dispose(); }
    }
}
