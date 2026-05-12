#nullable enable
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using SnmpMonitor.Config;
using SnmpMonitor.Logging;
using SnmpMonitor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SnmpMonitor.Snmp
{
    /// <summary>
    /// Универсальный SNMP менеджер с конфигурацией из внешних файлов
    /// </summary>
    public class UniversalSnmpManager
    {
        private readonly string _targetIp;
        private readonly string _community;
        private readonly ILogger _logger;
        private readonly OidConfiguration _oidConfig;

        public UniversalSnmpManager(string targetIp, string community, ILogger? logger = null, string? oidConfigPath = null)
        {
            _targetIp = targetIp;
            _community = community;
            _logger = logger ?? new ConsoleLogger();
            _oidConfig = OidConfigLoader.Load(oidConfigPath ?? "Config/snmp-tables.json");
        }

        /// <summary>
        /// Получить скалярное значение по категории и имени
        /// </summary>
        public string GetScalar(string category, string name)
        {
            try
            {
                string oid = OidConfigLoader.GetScalarOid(category, name);
                _logger.Debug("Запрос OID: {0} ({1}.{2})", oid, category, name);
                
                var version = VersionCode.V2;
                var endPoint = new IPEndPoint(IPAddress.Parse(_targetIp), 161);
                var communityParam = new OctetString(_community);
                var oidList = new List<Variable> { new Variable(new ObjectIdentifier(oid)) };
                
                var result = Messenger.Get(version, endPoint, communityParam, oidList, null!);
                
                if (result != null && result.Count > 0)
                {
                    var variable = result[0];
                    string value = DecodeRawData(variable.Data.ToString(), variable.Data);
                    _logger.Debug("Получено: {0} = {1}", oid, value);
                    return value;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("Ошибка при запросе {0}.{1}: {2}", category, name, ex.Message);
            }
            return null!;
        }

        /// <summary>
        /// Получить скалярное значение как число
        /// </summary>
        public ulong GetScalarAsLong(string category, string name)
        {
            string rawStr = GetScalar(category, name);
            return ParseToULong(rawStr);
        }

        /// <summary>
        /// Универсальный метод для получения данных таблицы
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> WalkTable(string tableKey)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            
            try
            {
                TableConfig tableConfig = OidConfigLoader.GetTableConfig(tableKey);
                _logger.Debug("Walk таблицы: {0} (OID: {1})", tableConfig.Name, tableConfig.BaseOid);
                
                // Собираем данные для каждого поля
                var fieldData = new Dictionary<string, Dictionary<string, string>>();
                
                // Загружаем общий справочник значений для таблицы если указан
                Dictionary<string, string>? tableValueMap = null;
                if (!string.IsNullOrEmpty(tableConfig.ValueMapping))
                {
                    tableValueMap = OidConfigLoader.LoadValueMapping(tableConfig.ValueMapping);
                }
                
                foreach (var field in tableConfig.Fields)
                {
                    // Загружаем индивидуальный справочник для поля если указан (переопределяет таблицу)
                    Dictionary<string, string>? fieldValueMap = null;
                    if (!string.IsNullOrEmpty(field.ValueMapping))
                    {
                        fieldValueMap = OidConfigLoader.LoadValueMapping(field.ValueMapping);
                    }
                    
                    var walkResult = WalkSingleField(field.Oid, field.Type, field.Format, field.Map, fieldValueMap ?? tableValueMap);
                    fieldData[field.Name] = walkResult;
                }
                
                // Определяем индексы (объединяем все ключи)
                var allIndexes = new HashSet<string>();
                foreach (var field in fieldData.Values)
                {
                    foreach (var key in field.Keys)
                    {
                        allIndexes.Add(key);
                    }
                }
                
                // Формируем результат по индексам
                foreach (var index in allIndexes)
                {
                    var entry = new Dictionary<string, string>();
                    foreach (var fieldName in fieldData.Keys)
                    {
                        if (fieldData[fieldName].TryGetValue(index, out var value))
                        {
                            entry[fieldName] = value;
                        }
                    }
                    result[index] = entry;
                }
                
                _logger.Debug("Walk {0}: получено {1} записей", tableConfig.Name, result.Count);
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка Walk таблицы {0}: {1}", tableKey, ex);
            }
            
            return result;
        }

        /// <summary>
        /// Walk одного поля таблицы
        /// </summary>
        private Dictionary<string, string> WalkSingleField(string rootOid, string? fieldType = null, string? format = null, Dictionary<string, string>? map = null, Dictionary<string, string>? valueMapping = null)
        {
            var result = new Dictionary<string, string>();
            
            try
            {
                var version = VersionCode.V2;
                var endPoint = new IPEndPoint(IPAddress.Parse(_targetIp), 161);
                var communityParam = new OctetString(_community);
                var rootOidObj = new ObjectIdentifier(rootOid);
                
                var snmpResult = Messenger.Walk(version, endPoint, communityParam, rootOidObj, WalkMode.WithinSubtree, null!);
                
                if (snmpResult == null) return result;

                foreach (var variable in snmpResult)
                {
                    string fullOid = variable.Id.ToString();
                    string index = fullOid.Substring(rootOid.Length);
                    if (index.StartsWith(".")) index = index.Substring(1);
                    
                    // Для полей типа "index" значение берётся из индекса OID
                    if (fieldType == "index")
                    {
                        // Просто возвращаем индекс как есть (число или строка)
                        result[index] = index;
                    }
                    // Для полей типа "ipaddr" декодируем IP адрес из значения
                    else if (fieldType == "ipaddr")
                    {
                        string decodedValue;
                        
                        // Пробуем получить байты из OctetString напрямую для IP адреса
                        if (variable.Data is OctetString octetStr && octetStr.Length == 4)
                        {
                            byte[] bytes = new byte[4];
                            for (int i = 0; i < 4; i++)
                            {
                                bytes[i] = octetStr[i];
                            }
                            decodedValue = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
                        }
                        // Пробуем декодировать IP адрес из индекса OID (альтернативный формат)
                        else if (index.Contains("."))
                        {
                            // IP адрес закодирован в индексе OID (например, .192.168.1.1)
                            string[] parts = index.Split('.');
                            if (parts.Length >= 4)
                            {
                                try
                                {
                                    byte[] octets = new byte[4];
                                    bool allParsed = true;
                                    for (int i = 0; i < 4; i++)
                                    {
                                        if (!byte.TryParse(parts[i], out octets[i]))
                                        {
                                            allParsed = false;
                                            break;
                                        }
                                    }
                                    if (allParsed)
                                    {
                                        decodedValue = $"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]}";
                                    }
                                    else
                                    {
                                        string rawValueFallback = variable.Data.ToString();
                                        decodedValue = DecodeRawData(rawValueFallback, variable.Data);
                                    }
                                }
                                catch
                                {
                                    string rawValueFallback = variable.Data.ToString();
                                    decodedValue = DecodeRawData(rawValueFallback, variable.Data);
                                }
                            }
                            else
                            {
                                string rawValueFallback = variable.Data.ToString();
                                decodedValue = DecodeRawData(rawValueFallback, variable.Data);
                            }
                        }
                        else
                        {
                            // Стандартное декодирование
                            string rawValue = variable.Data.ToString();
                            decodedValue = DecodeRawData(rawValue, variable.Data);
                        }
                        
                        result[index] = decodedValue;
                    }
                    // Для полей типа "macaddress" декодируем MAC адрес из OctetString
                    else if (fieldType == "macaddress")
                    {
                        string decodedValue;
                        
                        // Получаем байты из OctetString для MAC адреса (6 байт)
                        if (variable.Data is OctetString macOctetStr && macOctetStr.Length >= 6)
                        {
                            byte[] bytes = new byte[6];
                            for (int i = 0; i < 6; i++)
                            {
                                bytes[i] = macOctetStr[i];
                            }
                            decodedValue = $"{bytes[0]:X2}-{bytes[1]:X2}-{bytes[2]:X2}-{bytes[3]:X2}-{bytes[4]:X2}-{bytes[5]:X2}";
                        }
                        else
                        {
                            // Стандартное декодирование с попыткой извлечь байты
                            string rawValue = variable.Data.ToString();
                            byte[] rawBytes = DecodeRawDataToBytes(rawValue, variable.Data);
                            
                            if (rawBytes != null && rawBytes.Length >= 6)
                            {
                                decodedValue = $"{rawBytes[0]:X2}-{rawBytes[1]:X2}-{rawBytes[2]:X2}-{rawBytes[3]:X2}-{rawBytes[4]:X2}-{rawBytes[5]:X2}";
                            }
                            else
                            {
                                decodedValue = rawValue;
                            }
                        }
                        
                        result[index] = decodedValue;
                    }
                    // Для числовых полей (long, int, uint, ulong) с форматированием ИЛИ valueMapping
                    else if ((fieldType == "long" || fieldType == "int" || fieldType == "uint" || fieldType == "ulong"))
                    {
                        // Берем значение напрямую из числового типа SNMP
                        string? numericValue = null;
                        if (variable.Data is Gauge32 gauge32)
                        {
                            numericValue = gauge32.UInt32.ToString();
                        }
                        else if (variable.Data is Integer32 asnInt)
                        {
                            numericValue = asnInt.ToInt32().ToString();
                        }
                        else if (variable.Data is Counter32 counter32)
                        {
                            numericValue = counter32.UInt32.ToString();
                        }
                        else if (variable.Data is Counter64 counter64)
                        {
                            numericValue = counter64.UInt64.ToString();
                        }
                        else
                        {
                            // Пытаемся получить строковое представление и распарсить
                            string rawValue = variable.Data.ToString();
                            numericValue = DecodeRawData(rawValue, variable.Data);
                        }
                        
                        // Если numericValue все еще null или пустой, пробуем распарсить строку как число для форматирования
                        if (string.IsNullOrEmpty(numericValue) && !string.IsNullOrEmpty(format))
                        {
                            string rawValue = variable.Data.ToString();
                            // Пытаемся распарсить сырую строку как число
                            if (ulong.TryParse(rawValue, out ulong parsedValue))
                            {
                                numericValue = parsedValue.ToString();
                            }
                        }
                        
                        // Сначала применяем форматирование если указано
                        if (!string.IsNullOrEmpty(format))
                        {
                            if (!string.IsNullOrEmpty(numericValue))
                            {
                                result[index] = ApplyFormat(numericValue, format);
                            }
                            else
                            {
                                result[index] = "N/A";
                            }
                        }
                        // Затем применяем valueMapping если указано (и нет форматирования)
                        else if (valueMapping != null && !string.IsNullOrEmpty(numericValue))
                        {
                            if (valueMapping.TryGetValue(numericValue, out var mappedValue))
                            {
                                result[index] = mappedValue;
                            }
                            else
                            {
                                result[index] = numericValue;
                            }
                        }
                        else
                        {
                            result[index] = numericValue ?? "N/A";
                        }
                    }
                    // Для полей типа oid с valueMapping
                    else if (fieldType == "oid" && valueMapping != null)
                    {
                        string rawValue = variable.Data.ToString();
                        // OID может приходить с ведущей точкой или без - нормализуем
                        string decodedValue = rawValue.TrimStart('.');
                        
                        // Пробуем найти в справочнике сначала как есть, затем с ведущей точкой
                        string mappedValue;
                        if (!valueMapping.TryGetValue(decodedValue, out mappedValue))
                        {
                            string altKey = rawValue.StartsWith(".") ? rawValue.Substring(1) : "." + rawValue;
                            valueMapping.TryGetValue(altKey, out mappedValue);
                        }
                        
                        result[index] = mappedValue ?? rawValue;
                    }
                    else
                    {
                        // Декодирование с учетом кодировки и формата
                        string rawValue = variable.Data.ToString();
                        string decodedValue = DecodeRawData(rawValue, variable.Data);
                        
                        // Применяем справочник значений (valueMapping) если указан
                        // valueMapping используется для преобразования числовых кодов в названия (например, ifType: 6 -> ethernetCsmacd)
                        if (valueMapping != null && valueMapping.TryGetValue(decodedValue, out var mappedValue))
                        {
                            decodedValue = mappedValue;
                        }
                        // Применяем встроенный маппинг если указан (для int типов)
                        else if (map != null && map.TryGetValue(decodedValue, out var inlineMappedValue))
                        {
                            decodedValue = inlineMappedValue;
                        }
                        
                        result[index] = decodedValue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("Ошибка Walk {0}: {1}", rootOid, ex.Message);
            }
            return result;
        }

        /// <summary>
        /// Декодирование индекса OID в IP адрес
        /// Поддерживает несколько форматов:
        /// 1. Числовой: "192.168.1.1" или ".192.168.1.1"
        /// 2. Бинарный: строка где каждый символ = один байт IP
        /// 3. UTF-8 encoded bytes: когда байты IP были интерпретированы как UTF-8 текст
        /// </summary>
        private string DecodeIndexToIpAddress(string index)
        {
            if (string.IsNullOrEmpty(index)) return index;
            
            // Удаляем ведущую точку если есть
            if (index.StartsWith("."))
            {
                index = index.Substring(1);
            }
            
            // Формат 1: Числовой формат с точками (например, "192.168.1.1")
            if (index.Contains("."))
            {
                string[] parts = index.Split('.');
                
                // Для IP адреса ожидаем 4 октета
                if (parts.Length >= 4)
                {
                    try
                    {
                        byte[] octets = new byte[4];
                        bool allParsed = true;
                        for (int i = 0; i < 4; i++)
                        {
                            if (!byte.TryParse(parts[i], out octets[i]))
                            {
                                allParsed = false;
                                break;
                            }
                        }
                        if (allParsed)
                        {
                            return $"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]}";
                        }
                    }
                    catch
                    {
                        // Ошибка парсинга, пробуем другие форматы
                    }
                }
            }
            
            // Формат 2: Бинарный формат - каждый символ строки представляет байт IP адреса
            if (index.Length >= 4)
            {
                try
                {
                    byte[] octets = new byte[4];
                    bool allValid = true;
                    for (int i = 0; i < 4; i++)
                    {
                        int codePoint = index[i];
                        if (codePoint > 255)
                        {
                            allValid = false;
                            break;
                        }
                        octets[i] = (byte)codePoint;
                    }
                    // Для IP адресов и масок принимаем любой диапазон (0-255 для первого октета)
                    // Маски могут быть 0.0.0.0, сети могут начинаться с 0 (по умолчанию)
                    if (allValid)
                    {
                        return $"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]}";
                    }
                }
                catch
                {
                    // Ошибка, пробуем следующий формат
                }
            }
            
            // Формат 3: UTF-8 encoded bytes
            // Когда байты IP адреса были неправильно интерпретированы как UTF-8 текст
            try
            {
                byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(index);
                if (utf8Bytes.Length >= 4)
                {
                    // Принимаем любой диапазон для первого октета (0-255)
                    return $"{utf8Bytes[0]}.{utf8Bytes[1]}.{utf8Bytes[2]}.{utf8Bytes[3]}";
                }
            }
            catch
            {
                // Ошибка, возвращаем как есть
            }
            
            return index;
        }

        /// <summary>
        /// Применение форматирования к значению
        /// </summary>
        private string ApplyFormat(string value, string format)
        {
            if (string.IsNullOrEmpty(value)) return value;
            
            switch (format.ToLower())
            {
                case "speed_mbps":
                    // Форматирование скорости из Мбит/с в человекочитаемый формат
                    // ifHighSpeed (.1.3.6.1.2.1.31.1.1.1.15) возвращается в Мбит/с
                    if (ulong.TryParse(value, out ulong speedMbps))
                    {
                        if (speedMbps == 0)
                            return "0 bps";
                        if (speedMbps >= 1_000)
                            return $"{speedMbps / 1_000.0:F1} Gb/s";
                        return $"{speedMbps} Mb/s";
                    }
                    // Если значение не числовое (например, OID или строка), возвращаем "N/A"
                    return "N/A";
            }
            
            return value;
        }

        /// <summary>
        /// Декодирование значения с поддержкой различных типов данных
        /// </summary>
        private string DecodeRawData(string input, ISnmpData? asnValue = null)
        {
            if (string.IsNullOrEmpty(input)) return input;
            
            // Проверяем тип ASN.1 для правильного декодирования
            if (asnValue != null)
            {
                // Обработка Integer/Integer32 - возвращаем числовое значение
                if (asnValue is Integer32 asnInt)
                {
                    return asnInt.ToInt32().ToString();
                }
                
                // Обработка Counter32
                if (asnValue is Counter32 counter32)
                {
                    return counter32.UInt32.ToString();
                }
                
                // Обработка Counter64 для больших чисел
                if (asnValue is Counter64 counter64)
                {
                    return counter64.UInt64.ToString();
                }
                
                // Обработка Gauge32
                if (asnValue is Gauge32 gauge32)
                {
                    return gauge32.UInt32.ToString();
                }
                
                // Обработка OctetString - может содержать IP адрес в бинарном формате или текст
                if (asnValue is OctetString octetStr)
                {
                    try
                    {
                        // Получаем байты через индексатор или метод ToByteArray
                        byte[] bytes = new byte[octetStr.Length];
                        for (int i = 0; i < octetStr.Length; i++)
                        {
                            bytes[i] = octetStr[i];
                        }
                        
                        // Проверяем, является ли это IP адресом (4 байта)
                        if (bytes.Length == 4)
                        {
                            return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
                        }
                        
                        if (bytes != null && bytes.Length > 0)
                        {
                            string utf8Str = Encoding.UTF8.GetString(bytes);
                            // Проверяем, является ли строка читаемой
                            if (utf8Str.Any(c => c >= 32 && c < 127) || utf8Str.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c)))
                                return utf8Str.Trim();
                        }
                    }
                    catch { }
                }
            }
            
            // Стандартная обработка шестнадцатеричных данных
            if (input.Contains(".") || input.Contains(":")) return input;

            string clean = input.Replace(" ", "");
            if (IsHex(clean))
            {
                try
                {
                    byte[] data = new byte[clean.Length / 2];
                    for (int i = 0; i < clean.Length; i += 2)
                        data[i / 2] = Convert.ToByte(clean.Substring(i, 2), 16);

                    if (data.Length == 6)
                        return string.Join(":", data.Select(b => b.ToString("X2")));
                    if (data.Length == 4)
                        return string.Join(".", data);

                    string decoded = Encoding.UTF8.GetString(data);
                    if (decoded.All(c => c >= 32 || char.IsWhiteSpace(c)))
                        return decoded.Trim();
                }
                catch { }
            }
            return input;
        }

        /// <summary>
        /// Декодирование значения в байты с поддержкой различных типов данных
        /// </summary>
        private byte[]? DecodeRawDataToBytes(string input, ISnmpData? asnValue = null)
        {
            if (asnValue != null)
            {
                // Обработка OctetString - получаем байты напрямую
                if (asnValue is OctetString octetStr)
                {
                    try
                    {
                        byte[] bytes = new byte[octetStr.Length];
                        for (int i = 0; i < octetStr.Length; i++)
                        {
                            bytes[i] = octetStr[i];
                        }
                        return bytes;
                    }
                    catch { }
                }
            }
            
            // Стандартная обработка шестнадцатеричных данных
            if (string.IsNullOrEmpty(input)) return null;
            if (input.Contains(".") || input.Contains(":")) return null;

            string clean = input.Replace(" ", "");
            if (IsHex(clean))
            {
                try
                {
                    byte[] data = new byte[clean.Length / 2];
                    for (int i = 0; i < clean.Length; i += 2)
                        data[i / 2] = Convert.ToByte(clean.Substring(i, 2), 16);
                    return data;
                }
                catch { }
            }
            
            return null;
        }

        /// <summary>
        /// Получить системную информацию
        /// </summary>
        public SystemInfo GetSystemInfo()
        {
            _logger.Info("📋 Сбор системной информации...");
            return new SystemInfo
            {
                SysDescr = GetScalar("system", "SysDescr"),
                SysName = GetScalar("system", "SysName"),
                SysUpTime = GetScalar("system", "SysUpTime"),
                SysContact = GetScalar("system", "SysContact"),
                SysLocation = GetScalar("system", "SysLocation")
            };
        }

        /// <summary>
        /// Получить данные интерфейсов
        /// </summary>
        public List<InterfaceInfo> GetInterfaces()
        {
            _logger.Info("🔄 Сбор данных интерфейсов...");
            var list = new List<InterfaceInfo>();
            
            var tableData = WalkTable("interfaces");
            foreach (var kvp in tableData)
            {
                var info = new InterfaceInfo();
                if (kvp.Value.ContainsKey("Index")) info.Index = ParseInt(kvp.Value["Index"]);
                if (kvp.Value.ContainsKey("Description")) info.Description = kvp.Value["Description"];
                if (kvp.Value.ContainsKey("Type")) info.Type = ParseInt(kvp.Value["Type"]);
                if (kvp.Value.ContainsKey("Mtu")) info.Mtu = ParseLong(kvp.Value["Mtu"]);
                if (kvp.Value.ContainsKey("Speed")) info.Speed = ParseULong(kvp.Value["Speed"]);
                if (kvp.Value.ContainsKey("AdminStatus")) info.AdminStatus = ParseInt(kvp.Value["AdminStatus"]);
                if (kvp.Value.ContainsKey("OperStatus")) info.OperStatus = ParseInt(kvp.Value["OperStatus"]);
                if (kvp.Value.ContainsKey("InOctets")) info.InOctets = ParseULong(kvp.Value["InOctets"]);
                if (kvp.Value.ContainsKey("OutOctets")) info.OutOctets = ParseULong(kvp.Value["OutOctets"]);
                if (kvp.Value.ContainsKey("InErrors")) info.InErrors = ParseUInt(kvp.Value["InErrors"]);
                if (kvp.Value.ContainsKey("OutErrors")) info.OutErrors = ParseUInt(kvp.Value["OutErrors"]);
                list.Add(info);
            }
            
            list = list.OrderBy(i => i.Index).ToList();
            _logger.Info("   Найдено интерфейсов: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить IP адреса
        /// </summary>
        public List<IpAddressInfo> GetIpAddresses()
        {
            _logger.Info("📡 Сбор IP-адресов...");
            var list = new List<IpAddressInfo>();
            
            var tableData = WalkTable("ipAddresses");
            foreach (var kvp in tableData)
            {
                var info = new IpAddressInfo();
                if (kvp.Value.ContainsKey("Address")) info.Address = kvp.Value["Address"];
                if (kvp.Value.ContainsKey("Mask")) info.Mask = kvp.Value["Mask"];
                if (kvp.Value.ContainsKey("IfIndex")) info.IfIndex = ParseInt(kvp.Value["IfIndex"]);
                
                // Валидация IP адреса перед добавлением
                if (!IsValidIpAddress(info.Address))
                {
                    _logger.Warn("Некорректный IP адрес: {0}, запись пропущена", info.Address);
                    continue;
                }
                
                // Валидация маски подсети
                if (!IsValidIpAddress(info.Mask))
                {
                    _logger.Warn("Некорректная маска подсети: {0} для IP {1}, запись пропущена", info.Mask, info.Address);
                    continue;
                }
                
                list.Add(info);
            }
            
            _logger.Info("   Найдено IP: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить ARP таблицу
        /// </summary>
        public List<ArpEntry> GetArpTable()
        {
            _logger.Info("🌐 Сбор ARP таблицы...");
            var list = new List<ArpEntry>();
            
            var tableData = WalkTable("arpTable");
            foreach (var kvp in tableData)
            {
                var entry = new ArpEntry();
                if (kvp.Value.ContainsKey("IfIndex")) entry.IfIndex = ParseInt(kvp.Value["IfIndex"]);
                if (kvp.Value.ContainsKey("PhysAddress")) entry.Mac = kvp.Value["PhysAddress"];
                if (kvp.Value.ContainsKey("NetAddress")) entry.Ip = kvp.Value["NetAddress"];
                if (kvp.Value.ContainsKey("Type")) entry.Type = ParseInt(kvp.Value["Type"]);
                
                // Валидация IP адреса перед добавлением
                if (!IsValidIpAddress(entry.Ip))
                {
                    _logger.Warn("Некорректный IP адрес в ARP: {0}, запись пропущена", entry.Ip);
                    continue;
                }
                
                // Валидация MAC адреса
                if (!IsValidMacAddress(entry.Mac))
                {
                    _logger.Warn("Некорректный MAC адрес в ARP: {0} для IP {1}, запись пропущена", entry.Mac, entry.Ip);
                    continue;
                }
                
                // Фильтруем только динамические записи (type != 2)
                if (entry.Type != 2) list.Add(entry);
            }
            
            _logger.Info("   Найдено ARP записей: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить таблицу маршрутизации
        /// </summary>
        public List<RouteEntry> GetRoutingTable()
        {
            _logger.Info("🛣️ Сбор таблицы маршрутизации...");
            var list = new List<RouteEntry>();
            
            var tableData = WalkTable("routingTable");
            foreach (var kvp in tableData)
            {
                var entry = new RouteEntry();
                if (kvp.Value.ContainsKey("Dest")) entry.Dest = kvp.Value["Dest"];
                if (kvp.Value.ContainsKey("Mask")) entry.Mask = kvp.Value["Mask"];
                if (kvp.Value.ContainsKey("NextHop")) entry.NextHop = kvp.Value["NextHop"];
                if (kvp.Value.ContainsKey("IfIndex")) entry.IfIndex = ParseInt(kvp.Value["IfIndex"]);
                if (kvp.Value.ContainsKey("Type")) entry.Type = ParseInt(kvp.Value["Type"]) == 3 ? "direct" : "indirect";
                if (kvp.Value.ContainsKey("Proto")) entry.Proto = ParseInt(kvp.Value["Proto"]) == 2 ? "local" : ParseInt(kvp.Value["Proto"]).ToString();
                if (kvp.Value.ContainsKey("Metric")) entry.Metric = ParseInt(kvp.Value["Metric"]);
                if (kvp.Value.ContainsKey("Age")) entry.Age = ParseInt(kvp.Value["Age"]);
                
                // Валидация IP адреса назначения
                if (!IsValidIpAddress(entry.Dest))
                {
                    _logger.Warn("Некорректный IP адрес назначения: {0}, запись пропущена", entry.Dest);
                    continue;
                }
                
                // Валидация маски подсети
                if (!IsValidIpAddress(entry.Mask))
                {
                    _logger.Warn("Некорректная маска подсети: {0} для маршрута {1}, запись пропущена", entry.Mask, entry.Dest);
                    continue;
                }
                
                // Валидация NextHop если указан
                if (!string.IsNullOrEmpty(entry.NextHop) && !IsValidIpAddress(entry.NextHop))
                {
                    _logger.Warn("Некорректный NextHop: {0} для маршрута {1}, запись пропущена", entry.NextHop, entry.Dest);
                    continue;
                }
                
                list.Add(entry);
            }
            
            _logger.Info("   Найдено маршрутов: {0}", list.Count);
            return list;
        }
        
        /// <summary>
        /// Проверка корректности IPv4 адреса
        /// </summary>
        private static bool IsValidIpAddress(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;

            string[] parts = ip.Split('.');
            if (parts.Length != 4) return false;

            foreach (var part in parts)
            {
                if (!byte.TryParse(part, out _))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Проверка корректности MAC адреса
        /// Поддерживаемые форматы: XX-XX-XX-XX-XX-XX, XX:XX:XX:XX:XX:XX, XXXXXXXXXXXX
        /// </summary>
        private static bool IsValidMacAddress(string mac)
        {
            if (string.IsNullOrEmpty(mac)) return false;

            // Формат с дефисами: XX-XX-XX-XX-XX-XX
            if (Regex.IsMatch(mac, @"^([0-9A-Fa-f]{2}-){5}[0-9A-Fa-f]{2}$"))
                return true;

            // Формат с двоеточиями: XX:XX:XX:XX:XX:XX
            if (Regex.IsMatch(mac, @"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$"))
                return true;

            // Формат без разделителей: XXXXXXXXXXXX
            if (Regex.IsMatch(mac, @"^[0-9A-Fa-f]{12}$"))
                return true;

            return false;
        }

        /// <summary>
        /// Получить информацию о дисках
        /// </summary>
        public List<DiskInfo> GetStorageInfo()
        {
            _logger.Info("💾 Сбор информации о дисках...");
            var list = new List<DiskInfo>();
            
            var tableData = WalkTable("storage");
            foreach (var kvp in tableData)
            {
                double unitFactor = kvp.Value.ContainsKey("Units") ? ParseLong(kvp.Value["Units"]) : 1024;
                if (unitFactor == 0) unitFactor = 1024;
                
                double sizeVal = kvp.Value.ContainsKey("Size") ? ParseLong(kvp.Value["Size"]) : 0;
                double usedVal = kvp.Value.ContainsKey("Used") ? ParseLong(kvp.Value["Used"]) : 0;
                
                if (sizeVal > 1000 && kvp.Value.ContainsKey("Descr"))
                {
                    list.Add(new DiskInfo
                    {
                        Description = kvp.Value["Descr"],
                        TotalMB = Math.Round((sizeVal * unitFactor) / 1048576, 0),
                        UsedMB = Math.Round((usedVal * unitFactor) / 1048576, 0),
                        Percent = sizeVal > 0 ? Math.Round((usedVal * 100.0 / sizeVal), 1) : 0
                    });
                }
            }
            
            _logger.Info("   Найдено дисков: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить загрузку CPU
        /// </summary>
        public List<CpuCore> GetCPULoad()
        {
            _logger.Info("⚡ Сбор данных CPU...");
            var list = new List<CpuCore>();
            
            var tableData = WalkTable("cpu");
            int count = 0;
            foreach (var kvp in tableData)
            {
                int load = kvp.Value.ContainsKey("Load") ? ParseInt(kvp.Value["Load"]) : 0;
                list.Add(new CpuCore { Index = count++, Load = load });
            }
            
            _logger.Info("   Найдено ядер CPU: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить список процессов
        /// </summary>
        public List<ProcessInfo> GetProcesses()
        {
            _logger.Info("📋 Сбор списка процессов...");
            var list = new List<ProcessInfo>();
            
            var tableData = WalkTable("processes");
            int count = 0;
            foreach (var kvp in tableData)
            {
                if (count >= SnmpConfig.MaxProcesses) break;
                
                var proc = new ProcessInfo();
                if (kvp.Value.ContainsKey("Name")) proc.Name = kvp.Value["Name"];
                if (kvp.Value.ContainsKey("Path")) proc.Path = kvp.Value["Path"];
                if (kvp.Value.ContainsKey("Params")) proc.Params = kvp.Value["Params"];
                if (kvp.Value.ContainsKey("Type"))
                {
                    int typeVal = ParseInt(kvp.Value["Type"]);
                    proc.Type = typeVal == 4 ? "App" : "System";
                }
                if (kvp.Value.ContainsKey("Status"))
                {
                    int statusVal = ParseInt(kvp.Value["Status"]);
                    proc.Status = (statusVal == 1 || statusVal == 2) ? "Running" : "Other";
                }
                list.Add(proc);
                count++;
            }
            
            _logger.Info("   Найдено процессов: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить информацию об устройствах
        /// </summary>
        public List<DeviceInfo> GetDevices()
        {
            _logger.Info("🔧 Сбор информации об устройствах...");
            var list = new List<DeviceInfo>();
            
            var tableData = WalkTable("devices");
            foreach (var kvp in tableData)
            {
                var device = new DeviceInfo();
                if (kvp.Value.ContainsKey("Type")) device.Type = ParseInt(kvp.Value["Type"]);
                if (device.Type > 0)
                {
                    if (kvp.Value.ContainsKey("Descr")) device.Description = kvp.Value["Descr"];
                    if (kvp.Value.ContainsKey("Status"))
                    {
                        int s = ParseInt(kvp.Value["Status"]);
                        device.Status = s == 2 ? "Running" : "Unknown";
                    }
                    if (kvp.Value.ContainsKey("Errors")) device.Errors = ParseUInt(kvp.Value["Errors"]);
                    list.Add(device);
                }
            }
            
            _logger.Info("   Найдено устройств: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить статистику протоколов
        /// </summary>
        public List<StatEntry> GetProtocolStats(string category, params string[] names)
        {
            var stats = new List<StatEntry>();
            foreach (var name in names)
            {
                stats.Add(new StatEntry
                {
                    Name = name,
                    Value = GetScalarAsLong(category, name).ToString("N0")
                });
            }
            return stats;
        }

        /// <summary>
        /// Получить скалярные значения категории как словарь
        /// </summary>
        public Dictionary<string, string> GetScalars(string category)
        {
            var result = new Dictionary<string, string>();
            try
            {
                var config = OidConfigLoader.Load();
                if (config.Scalars.ContainsKey(category))
                {
                    foreach (var kvp in config.Scalars[category])
                    {
                        result[kvp.Key] = GetScalar(category, kvp.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("Ошибка при запросе скаляров {0}: {1}", category, ex.Message);
            }
            return result;
        }

        /// <summary>
        /// Универсальный метод для получения любой таблицы по ключу конфигурации
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> GetUniversalTable(string tableKey)
        {
            return WalkTable(tableKey);
        }

        #region Helper Methods

        private bool IsHex(string input)
        {
            if (input.Length % 2 != 0) return false;
            foreach (char c in input)
                if (!"0123456789ABCDEFabcdef".Contains(c)) return false;
            return true;
        }

        private int ParseInt(string val) => int.TryParse(val, out int i) ? i : 0;
        private long ParseLong(string val) => long.TryParse(val, out long l) ? l : 0;
        private ulong ParseULong(string val) => ulong.TryParse(val, out ulong ul) ? ul : 0;
        private uint ParseUInt(string val) => uint.TryParse(val, out uint ui) ? ui : 0;
        
        private ulong ParseToULong(string rawStr)
        {
            if (ulong.TryParse(rawStr, out ulong result)) return result;

            string clean = rawStr.Replace(" ", "").Replace("(", "").Replace(")", "");
            if (clean.Length % 2 != 0 || !IsHex(clean)) return 0;

            try
            {
                byte[] data = new byte[clean.Length / 2];
                for (int i = 0; i < clean.Length; i += 2)
                    data[i / 2] = Convert.ToByte(clean.Substring(i, 2), 16);
                if (BitConverter.IsLittleEndian) Array.Reverse(data);
                if (data.Length == 4) return BitConverter.ToUInt32(data, 0);
                if (data.Length == 8) return BitConverter.ToUInt64(data, 0);
            }
            catch { }
            return 0;
        }

        #endregion
    }

    /// <summary>
    /// Модель системной информации
    /// </summary>
    public class SystemInfo
    {
        public string SysDescr { get; set; } = string.Empty;
        public string SysName { get; set; } = string.Empty;
        public string SysUpTime { get; set; } = string.Empty;
        public string SysContact { get; set; } = string.Empty;
        public string SysLocation { get; set; } = string.Empty;
    }
}
