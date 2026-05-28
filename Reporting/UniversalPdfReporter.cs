using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SnmpMonitor.Config;
using SnmpMonitor.Logging;
using SnmpMonitor.Models;

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
        public void ExportScalars(string groupId, Dictionary<string, string> values)
        {
            if (values == null || values.Count == 0) return;

            var scalarGroup = OidConfigLoader.GetScalarGroup(groupId);
            string displayName = scalarGroup?.DisplayName ?? groupId;
            
            string fileName = $"{groupId}_scalars.pdf";
            string filePath = Path.Combine(_outputPath, fileName);
            _logger.Info("Запись PDF: {0}", filePath);

            var headers = new[] { "Parameter", "Value" };
            var rows = new List<string[]>();

            foreach (var kvp in values)
            {
                rows.Add(new[] { kvp.Key, kvp.Value });
            }

            WriteSimpleTable(filePath, $"{displayName} - Scalar Values", headers, rows);
        }

        /// <summary>
        /// Универсальный экспорт таблицы на основе конфигурации
        /// </summary>
        public void ExportTable(string tableKey, Dictionary<string, Dictionary<string, string>> data)
        {
            if (data == null || data.Count == 0) return;

            var tableConfig = OidConfigLoader.GetTableById(tableKey);
            if (tableConfig == null)
            {
                _logger.Warn("Таблица '{0}' не найдена в конфигурации", tableKey);
                return;
            }

            string fileName = $"{tableKey}.pdf";
            string filePath = Path.Combine(_outputPath, fileName);
            _logger.Info("Запись PDF: {0}", filePath);

            // Заголовки из конфигурации колонок
            var headers = tableConfig.Columns.Select(c => c.Name).ToArray();
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
                    var columnConfig = tableConfig.Columns.FirstOrDefault(c => c.Name == fieldName);
                    row[i] = FormatValue(rawValue, columnConfig);
                }
                rows.Add(row);
            }

            WriteSimpleTable(filePath, tableConfig.Description ?? tableConfig.DisplayName, headers, rows);
        }

        /// <summary>
        /// Форматирование значения согласно конфигурации колонки
        /// </summary>
        private string FormatValue(string rawValue, ColumnDefinition? columnConfig)
        {
            if (string.IsNullOrEmpty(rawValue)) return "";
            
            if (columnConfig == null) return rawValue;
            
            // Применяем маппинг если указан
            if (!string.IsNullOrEmpty(columnConfig.MappingKey))
            {
                var mapping = OidConfigLoader.LoadValueMapping("Config/oid-mappings.json");
                if (mapping != null && mapping.TryGetValue(rawValue, out var mappedValue))
                {
                    return mappedValue;
                }
            }
            
            // Форматируем скорость (ifHighSpeed возвращается в Мбит/с)
            if (columnConfig.Format == "speed_mbps" && ulong.TryParse(rawValue, out ulong speedMbps))
            {
                if (speedMbps >= 1_000)
                    return $"{(speedMbps / 1_000.0):F1} Gbps";
                return $"{speedMbps} Mbps";
            }
            
            // Если значение не числовое (например, OID или строка), возвращаем N/A
            if (columnConfig.Format == "speed_mbps")
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
            var allTables = OidConfigLoader.GetAllTables();
            
            foreach (var table in allTables)
            {
                try
                {
                    var data = tableFetcher(table.Id);
                    if (data != null && data.Count > 0)
                    {
                        ExportTable(table.Id, data);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("Ошибка экспорта таблицы {0}: {1}", table.Id, ex.Message);
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
