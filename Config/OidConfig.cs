using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SnmpMonitor.Models;

namespace SnmpMonitor.Config
{
    /// <summary>
    /// Загрузчик конфигурации OID из внешних файлов
    /// </summary>
    public static class OidConfigLoader
    {
        private static OidConfiguration? _config;
        private static readonly object _lockObj = new();

        /// <summary>
        /// Загрузка конфигурации OID из JSON файла
        /// </summary>
        public static OidConfiguration Load(string configPath = "Config/snmp-tables.json")
        {
            lock (_lockObj)
            {
                if (_config != null) return _config;

                try
                {
                    string fullPath = Path.IsPathRooted(configPath) ? configPath : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath);
                    
                    if (!File.Exists(fullPath))
                    {
                        fullPath = Path.Combine(Directory.GetCurrentDirectory(), configPath);
                    }
                    
                    if (!File.Exists(fullPath))
                    {
                        Console.WriteLine($"⚠️ Файл конфигурации OID '{configPath}' не найден.");
                        _config = new OidConfiguration();
                    }
                    else
                    {
                        Console.WriteLine($"ℹ️ Загрузка конфигурации из: {fullPath}");
                        string json = File.ReadAllText(fullPath);
                        _config = JsonConvert.DeserializeObject<OidConfiguration>(json) ?? new OidConfiguration();
                        
                        Console.WriteLine($"✓ Загружено таблиц: {_config.Tables.Count}");
                        foreach (var table in _config.Tables)
                        {
                            string type = table.IsTable ? "Таблица" : "Скалярная группа";
                            Console.WriteLine($"  - {table.Id} ({type}): {table.Columns.Count} колонок");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка загрузки конфигурации OID: {ex.Message}");
                    _config = new OidConfiguration();
                }

                return _config;
            }
        }

        /// <summary>
        /// Сброс кэша конфигурации
        /// </summary>
        public static void Reset()
        {
            lock (_lockObj)
            {
                _config = null;
            }
        }

        /// <summary>
        /// Получить все таблицы (включая скалярные группы)
        /// </summary>
        public static List<TableDefinition> GetAllTables()
        {
            var config = Load();
            return config.Tables;
        }

        /// <summary>
        /// Получить только табличные объекты (isTable=true)
        /// </summary>
        public static List<TableDefinition> GetTableObjects()
        {
            var config = Load();
            return config.Tables.Where(t => t.IsTable).ToList();
        }

        /// <summary>
        /// Получить определение таблицы по ID
        /// </summary>
        public static TableDefinition? GetTableById(string tableId)
        {
            var config = Load();
            return config.Tables.FirstOrDefault(t => 
                t.Id.Equals(tableId, StringComparison.OrdinalIgnoreCase) ||
                t.Category.Equals(tableId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Загрузить справочник значений из файла oid-mappings.json
        /// </summary>
        public static Dictionary<string, string>? LoadValueMapping(string? mappingFileName, string? baseDir = null)
        {
            if (string.IsNullOrEmpty(mappingFileName))
                return null;

            try
            {
                string fullPath = mappingFileName;
                if (!Path.IsPathRooted(mappingFileName))
                {
                    fullPath = Path.Combine(baseDir ?? Directory.GetCurrentDirectory(), mappingFileName);
                }

                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"⚠️ Файл маппинга '{mappingFileName}' не найден");
                    return null;
                }

                string json = File.ReadAllText(fullPath);
                var allMappings = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                
                // Возвращаем все маппинги плоским списком
                return allMappings?.SelectMany(kvp => kvp.Value)
                    .ToDictionary(k => k.Key, v => v.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка загрузки маппинга '{mappingFileName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Получить OID для скалярного параметра (для совместимости со старым кодом)
        /// </summary>
        public static string GetScalarOid(string category, string name)
        {
            var config = Load();
            
            // Ищем таблицу с isTable=false по category или id
            var scalarGroup = config.Tables
                .FirstOrDefault(t => !t.IsTable && 
                    (t.Category.Equals(category, StringComparison.OrdinalIgnoreCase) || 
                     t.Id.Equals(category, StringComparison.OrdinalIgnoreCase)));
            
            if (scalarGroup == null)
            {
                // Пробуем найти по имени колонки напрямую
                var column = config.Tables
                    .SelectMany(t => t.Columns)
                    .FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                
                return column?.Oid ?? string.Empty;
            }
            
            // Ищем колонку с нужным именем
            var targetColumn = scalarGroup.Columns
                .FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            
            return targetColumn?.Oid ?? string.Empty;
        }

        /// <summary>
        /// Получить скалярную группу по ID
        /// </summary>
        public static TableDefinition? GetScalarGroup(string groupId)
        {
            var config = Load();
            return config.Tables.FirstOrDefault(t => 
                !t.IsTable && 
                (t.Id.Equals(groupId, StringComparison.OrdinalIgnoreCase) ||
                 t.Category.Equals(groupId, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
