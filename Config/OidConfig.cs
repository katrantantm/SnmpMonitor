using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SnmpMonitor.Config
{
    /// <summary>
    /// Конфигурация OID для таблиц SNMP
    /// </summary>
    public class OidConfiguration
    {
        [JsonProperty("tables")]
        public Dictionary<string, TableConfig> Tables { get; set; } = new();

        [JsonProperty("scalars")]
        public Dictionary<string, Dictionary<string, string>> Scalars { get; set; } = new();
    }

    /// <summary>
    /// Конфигурация таблицы SNMP
    /// </summary>
    public class TableConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("baseOid")]
        public string BaseOid { get; set; } = string.Empty;

        [JsonProperty("fields")]
        public List<FieldConfig> Fields { get; set; } = new();

        [JsonProperty("typeMappingFile", NullValueHandling = NullValueHandling.Ignore)]
        public string? TypeMappingFile { get; set; }

        [JsonProperty("valueMapping", NullValueHandling = NullValueHandling.Ignore)]
        public string? ValueMapping { get; set; }
    }

    /// <summary>
    /// Конфигурация поля таблицы SNMP
    /// </summary>
    public class FieldConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("oid")]
        public string Oid { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("encoding", NullValueHandling = NullValueHandling.Ignore)]
        public string? Encoding { get; set; }

        [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
        public string? Format { get; set; }

        [JsonProperty("map", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string>? Map { get; set; }

        [JsonProperty("valueMapping", NullValueHandling = NullValueHandling.Ignore)]
        public string? ValueMapping { get; set; }
    }

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
        /// <param name="configPath">Путь к файлу конфигурации</param>
        /// <returns>Конфигурация OID</returns>
        public static OidConfiguration Load(string configPath = "Config/snmp-tables.json")
        {
            lock (_lockObj)
            {
                if (_config != null) return _config;

                try
                {
                    if (!File.Exists(configPath))
                    {
                        Console.WriteLine($"⚠️ Файл конфигурации OID '{configPath}' не найден.");
                        _config = CreateDefaultConfig();
                    }
                    else
                    {
                        string json = File.ReadAllText(configPath);
                        _config = JsonConvert.DeserializeObject<OidConfiguration>(json) ?? CreateDefaultConfig();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка загрузки конфигурации OID: {ex.Message}");
                    _config = CreateDefaultConfig();
                }

                return _config;
            }
        }

        /// <summary>
        /// Создание конфигурации по умолчанию (из жестко заданных OID)
        /// </summary>
        private static OidConfiguration CreateDefaultConfig()
        {
            return new OidConfiguration
            {
                Tables = new Dictionary<string, TableConfig>
                {
                    ["interfaces"] = new TableConfig
                    {
                        Name = "Interfaces",
                        Description = "Сетевые интерфейсы",
                        BaseOid = ".1.3.6.1.2.1.2.2.1",
                        Fields = new List<FieldConfig>
                        {
                            new() { Name = "Description", Oid = ".1.3.6.1.2.1.2.2.1.2", Type = "string" },
                            new() { Name = "Type", Oid = ".1.3.6.1.2.1.2.2.1.3", Type = "int" },
                            new() { Name = "Mtu", Oid = ".1.3.6.1.2.1.2.2.1.4", Type = "long" },
                            new() { Name = "Speed", Oid = ".1.3.6.1.2.1.2.2.1.5", Type = "ulong" },
                            new() { Name = "AdminStatus", Oid = ".1.3.6.1.2.1.2.2.1.7", Type = "int" },
                            new() { Name = "OperStatus", Oid = ".1.3.6.1.2.1.2.2.1.8", Type = "int" },
                            new() { Name = "InOctets", Oid = ".1.3.6.1.2.1.2.2.1.10", Type = "ulong" },
                            new() { Name = "OutOctets", Oid = ".1.3.6.1.2.1.2.2.1.16", Type = "ulong" },
                            new() { Name = "InErrors", Oid = ".1.3.6.1.2.1.2.2.1.14", Type = "uint" },
                            new() { Name = "OutErrors", Oid = ".1.3.6.1.2.1.2.2.1.20", Type = "uint" }
                        }
                    },
                    ["ipAddresses"] = new TableConfig
                    {
                        Name = "IP Addresses",
                        Description = "IP адреса",
                        BaseOid = ".1.3.6.1.2.1.4.20.1",
                        Fields = new List<FieldConfig>
                        {
                            new() { Name = "Address", Oid = ".1.3.6.1.2.1.4.20.1.1", Type = "index" },
                            new() { Name = "IfIndex", Oid = ".1.3.6.1.2.1.4.20.1.2", Type = "int" },
                            new() { Name = "Mask", Oid = ".1.3.6.1.2.1.4.20.1.3", Type = "string" }
                        }
                    },
                    ["arpTable"] = new TableConfig
                    {
                        Name = "ARP Table",
                        Description = "ARP таблица",
                        BaseOid = ".1.3.6.1.2.1.4.22.1",
                        Fields = new List<FieldConfig>
                        {
                            new() { Name = "IfIndex", Oid = ".1.3.6.1.2.1.4.22.1.1", Type = "int" },
                            new() { Name = "PhysAddress", Oid = ".1.3.6.1.2.1.4.22.1.2", Type = "macaddress" },
                            new() { Name = "NetAddress", Oid = ".1.3.6.1.2.1.4.22.1.3", Type = "ipaddr" },
                            new() { Name = "Type", Oid = ".1.3.6.1.2.1.4.22.1.4", Type = "int" }
                        }
                    },
                    ["routingTable"] = new TableConfig
                    {
                        Name = "Routing Table",
                        Description = "Таблица маршрутизации",
                        BaseOid = ".1.3.6.1.2.1.4.21.1",
                        Fields = new List<FieldConfig>
                        {
                            new() { Name = "Dest", Oid = ".1.3.6.1.2.1.4.21.1.1", Type = "index" },
                            new() { Name = "IfIndex", Oid = ".1.3.6.1.2.1.4.21.1.2", Type = "int" },
                            new() { Name = "NextHop", Oid = ".1.3.6.1.2.1.4.21.1.7", Type = "string" },
                            new() { Name = "Type", Oid = ".1.3.6.1.2.1.4.21.1.8", Type = "int" },
                            new() { Name = "Proto", Oid = ".1.3.6.1.2.1.4.21.1.9", Type = "int" },
                            new() { Name = "Metric", Oid = ".1.3.6.1.2.1.4.21.1.3", Type = "int" },
                            new() { Name = "Age", Oid = ".1.3.6.1.2.1.4.21.1.10", Type = "int" },
                            new() { Name = "Mask", Oid = ".1.3.6.1.2.1.4.21.1.11", Type = "string" }
                        }
                    },
                    ["storage"] = new TableConfig
                    {
                        Name = "Storage",
                        Description = "Информация о дисках",
                        BaseOid = ".1.3.6.1.2.1.25.2.3.1",
                        Fields = new List<FieldConfig>
                        {
                            new() { Name = "Descr", Oid = ".1.3.6.1.2.1.25.2.3.1.3", Type = "string" },
                            new() { Name = "Units", Oid = ".1.3.6.1.2.1.25.2.3.1.4", Type = "long" },
                            new() { Name = "Size", Oid = ".1.3.6.1.2.1.25.2.3.1.5", Type = "long" },
                            new() { Name = "Used", Oid = ".1.3.6.1.2.1.25.2.3.1.6", Type = "long" }
                        }
                    },
                    ["cpu"] = new TableConfig
                    {
                        Name = "CPU",
                        Description = "Загрузка процессора",
                        BaseOid = ".1.3.6.1.2.1.25.3.3.1",
                        Fields = new List<FieldConfig>
                        {
                            new() { Name = "Load", Oid = ".1.3.6.1.2.1.25.3.3.1.2", Type = "int" }
                        }
                    },
                    ["processes"] = new TableConfig
                    {
                        Name = "Processes",
                        Description = "Список процессов",
                        BaseOid = ".1.3.6.1.2.1.25.4.2.1",
                        Fields = new List<FieldConfig>
                        {
                            new() { Name = "Name", Oid = ".1.3.6.1.2.1.25.4.2.1.2", Type = "string" },
                            new() { Name = "Path", Oid = ".1.3.6.1.2.1.25.4.2.1.4", Type = "string" },
                            new() { Name = "Params", Oid = ".1.3.6.1.2.1.25.4.2.1.5", Type = "string" },
                            new() { Name = "Type", Oid = ".1.3.6.1.2.1.25.4.2.1.6", Type = "int" },
                            new() { Name = "Status", Oid = ".1.3.6.1.2.1.25.4.2.1.7", Type = "int" }
                        }
                    },
                    ["devices"] = new TableConfig
                    {
                        Name = "Devices",
                        Description = "Устройства системы",
                        BaseOid = ".1.3.6.1.2.1.25.3.2.1",
                        Fields = new List<FieldConfig>
                        {
                            new() { Name = "Type", Oid = ".1.3.6.1.2.1.25.3.2.1.2", Type = "string" },
                            new() { Name = "Descr", Oid = ".1.3.6.1.2.1.25.3.2.1.3", Type = "string" },
                            new() { Name = "Status", Oid = ".1.3.6.1.2.1.25.3.2.1.5", Type = "int" },
                            new() { Name = "Errors", Oid = ".1.3.6.1.2.1.25.3.2.1.6", Type = "uint" }
                        }
                    }
                },
                Scalars = new Dictionary<string, Dictionary<string, string>>
                {
                    ["system"] = new Dictionary<string, string>
                    {
                        ["SysDescr"] = ".1.3.6.1.2.1.1.1.0",
                        ["SysUpTime"] = ".1.3.6.1.2.1.1.3.0",
                        ["SysContact"] = ".1.3.6.1.2.1.1.4.0",
                        ["SysName"] = ".1.3.6.1.2.1.1.5.0",
                        ["SysLocation"] = ".1.3.6.1.2.1.1.6.0"
                    },
                    ["ipStats"] = new Dictionary<string, string>
                    {
                        ["IpForwarding"] = ".1.3.6.1.2.1.4.1.0",
                        ["IpInReceives"] = ".1.3.6.1.2.1.4.3.0",
                        ["IpOutRequests"] = ".1.3.6.1.2.1.4.10.0"
                    },
                    ["tcpStats"] = new Dictionary<string, string>
                    {
                        ["TcpMaxConn"] = ".1.3.6.1.2.1.6.4.0",
                        ["TcpInSegs"] = ".1.3.6.1.2.1.6.10.0",
                        ["TcpOutSegs"] = ".1.3.6.1.2.1.6.11.0"
                    },
                    ["udpStats"] = new Dictionary<string, string>
                    {
                        ["UdpInDatagrams"] = ".1.3.6.1.2.1.7.1.0",
                        ["UdpOutDatagrams"] = ".1.3.6.1.2.1.7.4.0"
                    },
                    ["icmpStats"] = new Dictionary<string, string>
                    {
                        ["IcmpInMsgs"] = ".1.3.6.1.2.1.5.1.0",
                        ["IcmpOutMsgs"] = ".1.3.6.1.2.1.5.14.0",
                        ["IcmpInEchos"] = ".1.3.6.1.2.1.5.8.0",
                        ["IcmpOutEchos"] = ".1.3.6.1.2.1.5.21.0"
                    },
                    ["snmpStats"] = new Dictionary<string, string>
                    {
                        ["SnmpInPkts"] = ".1.3.6.1.2.1.11.1.0",
                        ["SnmpOutPkts"] = ".1.3.6.1.2.1.11.2.0"
                    }
                }
            };
        }

        /// <summary>
        /// Сброс кэша конфигурации
        /// </summary>
        public static void Reset()
        {
            lock (_lockObj)
            {
                _config = null!;
            }
        }

        /// <summary>
        /// Получить OID скалярного значения по имени
        /// </summary>
        public static string GetScalarOid(string category, string name)
        {
            var config = Load();
            if (config.Scalars.ContainsKey(category) && config.Scalars[category].ContainsKey(name))
            {
                return config.Scalars[category][name];
            }
            // Возвращаем пустую строку вместо исключения для обработки в вызывающем коде
            return string.Empty;
        }

        /// <summary>
        /// Получить конфигурацию таблицы по ключу
        /// </summary>
        public static TableConfig GetTableConfig(string tableKey)
        {
            var config = Load();
            if (config.Tables.ContainsKey(tableKey))
            {
                return config.Tables[tableKey];
            }
            throw new KeyNotFoundException($"Конфигурация таблицы '{tableKey}' не найдена");
        }

        /// <summary>
        /// Загрузить файл маппинга типов для таблицы (устаревший метод, используется LoadValueMapping)
        /// </summary>
        [Obsolete("Используйте LoadValueMapping")]
        public static Dictionary<string, string>? LoadTypeMapping(string? mappingFileName, string? baseDir = null)
        {
            return LoadValueMapping(mappingFileName, baseDir);
        }

        /// <summary>
        /// Загрузить справочник значений из файла oid-mappings.json по имени секции
        /// </summary>
        public static Dictionary<string, string>? LoadValueMapping(string? mappingName, string? baseDir = null)
        {
            if (string.IsNullOrEmpty(mappingName))
                return null;

            try
            {
                string configDir = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = Path.Combine(configDir, "Config", "oid-mappings.json");
                
                if (!File.Exists(fullPath))
                {
                    fullPath = Path.Combine(configDir, "oid-mappings.json");
                }
                
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"⚠️ Файл справочника значений 'oid-mappings.json' не найден.");
                    return null;
                }

                string json = File.ReadAllText(fullPath);
                var mappingData = JsonConvert.DeserializeObject<dynamic>(json);
                
                if (mappingData != null && mappingData.mappings != null)
                {
                    var mappings = mappingData.mappings as Newtonsoft.Json.Linq.JObject;
                    if (mappings != null && mappings.TryGetValue(mappingName, out var mappingSection))
                    {
                        var sectionObj = mappingSection as Newtonsoft.Json.Linq.JObject;
                        if (sectionObj != null && sectionObj.TryGetValue("values", out var valuesToken))
                        {
                            var result = new Dictionary<string, string>();
                            var valuesObj = valuesToken as Newtonsoft.Json.Linq.JObject;
                            if (valuesObj != null)
                            {
                                foreach (var prop in valuesObj.Properties())
                                {
                                    result[prop.Name] = prop.Value?.ToString() ?? string.Empty;
                                }
                            }
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка загрузки справочника '{mappingName}': {ex.Message}");
            }

            return null;
        }
    }
}
