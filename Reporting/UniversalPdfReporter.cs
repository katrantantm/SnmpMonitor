using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SnmpMonitor.Config;
using SnmpMonitor.Logging;

namespace SnmpMonitor.Reporting
{
    /// <summary>
    /// Универсальный генератор PDF отчетов на основе конфигурации OID
    /// </summary>
    public class UniversalPdfReporter : IDisposable
    {
        private readonly string _outputPath;
        private readonly ILogger _logger;
        private bool _disposed;
        private readonly OidConfiguration _config;

        public UniversalPdfReporter(string outputPath, ILogger? logger = null, string? configPath = null)
        {
            _outputPath = outputPath;
            _logger = logger ?? new ConsoleLogger();
            _config = OidConfigLoader.Load(configPath ?? "Config/snmp-tables.json");
            
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
            
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// Экспорт скалярных значений в PDF
        /// </summary>
        public void ExportScalars(string category, Dictionary<string, string> values)
        {
            if (!_config.Scalars.ContainsKey(category) || values == null || values.Count == 0) return;

            string fileName = $"{category}_scalars.pdf";
            string filePath = Path.Combine(_outputPath, fileName);
            _logger.Info("Запись PDF: {0}", filePath);

            var headers = new[] { "Parameter", "Value" };
            var rows = new List<string[]>();

            foreach (var kvp in values)
            {
                rows.Add(new[] { kvp.Key, kvp.Value });
            }

            WriteSimpleTable(filePath, $"{category} - Scalar Values", headers, rows);
        }

        /// <summary>
        /// Универсальный экспорт таблицы на основе конфигурации
        /// </summary>
        public void ExportTable(string tableKey, Dictionary<string, Dictionary<string, string>> data)
        {
            if (!_config.Tables.ContainsKey(tableKey) || data == null || data.Count == 0) return;

            var tableConfig = _config.Tables[tableKey];
            string fileName = $"{tableKey}.pdf";
            string filePath = Path.Combine(_outputPath, fileName);
            _logger.Info("Запись PDF: {0}", filePath);

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

            WriteSimpleTable(filePath, tableConfig.Description, headers, rows);
        }

        /// <summary>
        /// Форматирование значения согласно конфигурации поля
        /// </summary>
        private string FormatValue(string rawValue, FieldConfig? fieldConfig)
        {
            if (string.IsNullOrEmpty(rawValue)) return "";
            
            if (fieldConfig == null) return rawValue;
            
            // Применяем маппинг статусов
            if (fieldConfig.Map != null && fieldConfig.Map.TryGetValue(rawValue, out var mappedValue))
            {
                return mappedValue;
            }
            
            // Форматируем скорость (ifHighSpeed возвращается в Мбит/с)
            if (fieldConfig.Format == "speed_mbps" && ulong.TryParse(rawValue, out ulong speedMbps))
            {
                if (speedMbps >= 1_000)
                    return $"{(speedMbps / 1_000.0):F1} Gbps";
                return $"{speedMbps} Mbps";
            }
            
            // Если значение не числовое (например, OID или строка), возвращаем N/A
            if (fieldConfig.Format == "speed_mbps")
            {
                return "N/A";
            }
            
            return rawValue;
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

        private void WriteSimpleTable(string filePath, string title, string[] headers, List<string[]> rows)
        {
            _logger.Info("Запись PDF: {0}", filePath);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));
                    
                    page.Header()
                        .Text($"{title}\nGenerated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                        .SemiBold().FontSize(14).AlignCenter();
                    
                    page.Content()
                        .PaddingVertical(10)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                foreach (var header in headers)
                                {
                                    columns.RelativeColumn();
                                }
                            });

                            table.Header(header =>
                            {
                                foreach (var headerText in headers)
                                {
                                    header.Cell().Element(CellStyle).Text(headerText);
                                }
                                
                                static IContainer CellStyle(IContainer container) 
                                    => container.DefaultTextStyle(x => x.SemiBold()).Padding(3).BorderBottom(1).BorderColor(Colors.Black);
                            });

                            foreach (var row in rows)
                            {
                                foreach (var cellText in row)
                                {
                                    table.Cell().Element(CellStyleData).Text(cellText);
                                }
                                
                                static IContainer CellStyleData(IContainer container) 
                                    => container.Padding(3).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                            }
                        });
                    
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                });
            }).GeneratePdf(filePath);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _logger.Debug("Universal PDF Reporter освобожден");
            _disposed = true;
        }

        ~UniversalPdfReporter() { Dispose(); }
    }
}
