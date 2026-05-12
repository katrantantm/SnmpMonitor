using System;
using System.Collections.Generic;
using SnmpMonitor.Logging;
using SnmpMonitor.Snmp;

namespace SnmpMonitor.Reporting
{
    /// <summary>
    /// Демонстрационный класс для универсального экспорта SNMP данных в CSV и PDF
    /// </summary>
    public class UniversalReportGenerator : IDisposable
    {
        private readonly UniversalSnmpManager _snmp;
        private readonly UniversalCsvReporter _csvReporter;
        private readonly UniversalPdfReporter _pdfReporter;
        private readonly ILogger _logger;
        private bool _disposed = false;

        public UniversalReportGenerator(string targetIp, string community, string outputDir, ILogger? logger = null)
        {
            _logger = logger ?? new ConsoleLogger();
            _snmp = new UniversalSnmpManager(targetIp, community, _logger);
            _csvReporter = new UniversalCsvReporter(outputDir, _logger);
            _pdfReporter = new UniversalPdfReporter(outputDir, _logger);
        }

        /// <summary>
        /// Сгенерировать все отчеты (CSV и PDF) для всех таблиц и скаляров из конфигурации
        /// </summary>
        public void GenerateAllReports()
        {
            _logger.Info("🚀 Запуск универсального генератора отчетов...");

            // Экспорт скалярных значений
            ExportAllScalars();

            // Экспорт таблиц
            ExportAllTables();

            _logger.Info("✅ Генерация отчетов завершена");
        }

        /// <summary>
        /// Экспортировать все скалярные значения из конфигурации
        /// </summary>
        public void ExportAllScalars()
        {
            _logger.Info("📊 Экспорт скалярных значений...");
            
            var config = Config.OidConfigLoader.Load();
            foreach (var category in config.Scalars.Keys)
            {
                try
                {
                    var values = _snmp.GetScalars(category);
                    if (values.Count > 0)
                    {
                        _csvReporter.ExportScalars(category, values);
                        _pdfReporter.ExportScalars(category, values);
                        _logger.Info("   ✓ {0}: {1} значений", category, values.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("Ошибка экспорта скаляров {0}: {1}", category, ex.Message);
                }
            }
        }

        /// <summary>
        /// Экспортировать все таблицы из конфигурации
        /// </summary>
        public void ExportAllTables()
        {
            _logger.Info("📋 Экспорт таблиц...");
            
            var config = Config.OidConfigLoader.Load();
            foreach (var tableKey in config.Tables.Keys)
            {
                try
                {
                    var data = _snmp.GetUniversalTable(tableKey);
                    if (data.Count > 0)
                    {
                        _csvReporter.ExportTable(tableKey, data);
                        _pdfReporter.ExportTable(tableKey, data);
                        _logger.Info("   ✓ {0}: {1} записей", tableKey, data.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("Ошибка экспорта таблицы {0}: {1}", tableKey, ex.Message);
                }
            }
        }

        /// <summary>
        /// Экспортировать конкретную таблицу
        /// </summary>
        public void ExportTable(string tableKey)
        {
            var data = _snmp.GetUniversalTable(tableKey);
            if (data.Count > 0)
            {
                _csvReporter.ExportTable(tableKey, data);
                _pdfReporter.ExportTable(tableKey, data);
                _logger.Info("✓ Экспортирована таблица {0}: {1} записей", tableKey, data.Count);
            }
            else
            {
                _logger.Warn("Таблица {0} пуста или не найдена", tableKey);
            }
        }

        /// <summary>
        /// Экспортировать конкретную категорию скаляров
        /// </summary>
        public void ExportScalars(string category)
        {
            var values = _snmp.GetScalars(category);
            if (values.Count > 0)
            {
                _csvReporter.ExportScalars(category, values);
                _pdfReporter.ExportScalars(category, values);
                _logger.Info("✓ Экспортированы скаляры {0}: {1} значений", category, values.Count);
            }
            else
            {
                _logger.Warn("Скаляры {0} пусты или не найдены", category);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _csvReporter?.Dispose();
                    _pdfReporter?.Dispose();
                }
                _disposed = true;
            }
        }

        ~UniversalReportGenerator()
        {
            Dispose(false);
        }
    }
}
