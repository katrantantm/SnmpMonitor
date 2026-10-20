#nullable enable
using SnmpMonitor.Config;
using SnmpMonitor.Logging;
using SnmpMonitor.Models;
using SnmpMonitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SnmpMonitor.Snmp
{
    /// <summary>
    /// Универсальный SNMP менеджер с конфигурацией из внешних файлов.
    /// Тонкая обёртка над <see cref="UniversalSnmpClient"/> (низкоуровневые SNMP-операции):
    /// вся логика get/walk/декодирования сосредоточена в клиенте, здесь — фасад с
    /// историческим API (строки вместо Result&lt;T&gt;, значения по умолчанию "No Data"/0).
    /// </summary>
    public partial class UniversalSnmpManager : IDisposable
    {
        private const string NoData = "No Data";

        private readonly UniversalSnmpClient _client;
        private readonly ILogger _logger;

        public UniversalSnmpManager(string targetIp, string community, ILogger? logger = null, string? oidConfigPath = null)
        {
            _logger = logger ?? new ConsoleLogger();

            // Путь к конфигурации таблиц применяется через статический загрузчик,
            // если вызывающий указал нестандартный путь.
            if (!string.IsNullOrEmpty(oidConfigPath))
            {
                OidConfigLoader.Reset();
                OidConfigLoader.Load(oidConfigPath);
            }

            var snmpSettings = ConfigurationLoader.Load().Snmp;
            _client = new UniversalSnmpClient(
                targetIp,
                community,
                _logger,
                timeout: snmpSettings?.Timeout ?? 5000,
                version: snmpSettings?.Version ?? "v2c");
        }

        /// <summary>
        /// Основной низкоуровневый клиент (для переиспользования и тестов).
        /// </summary>
        public UniversalSnmpClient Client => _client;

        /// <summary>
        /// Получить скалярное значение по категории и имени.
        /// </summary>
        public string GetScalar(string category, string name)
        {
            var result = _client.GetScalar(category, name);
            return result.IsSuccess ? result.Value! : NoData;
        }

        /// <summary>
        /// Получить скалярное значение как число.
        /// </summary>
        public ulong GetScalarAsLong(string category, string name)
        {
            var result = _client.GetScalar(category, name);
            return result.IsSuccess ? ParseToULong(result.Value!) : 0UL;
        }

        /// <summary>
        /// Универсальный метод для получения данных таблицы.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> WalkTable(string tableKey)
        {
            var result = _client.WalkTable(tableKey);
            return result.IsSuccess ? result.Value! : new Dictionary<string, Dictionary<string, string>>();
        }

        /// <summary>
        /// Получить системную информацию.
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
        /// Получить данные интерфейсов.
        /// </summary>
        public List<InterfaceInfo> GetInterfaces()
        {
            _logger.Info("🔄 Сбор данных интерфейсов...");
            var list = new List<InterfaceInfo>();

            foreach (var kvp in WalkTable("interfaces"))
            {
                var info = new InterfaceInfo();
                if (kvp.Value.TryGetValue("Index", out var v)) info.Index = ParseInt(v);
                if (kvp.Value.TryGetValue("Description", out v)) info.Description = v;
                if (kvp.Value.TryGetValue("Type", out v)) info.Type = ParseInt(v);
                if (kvp.Value.TryGetValue("Mtu", out v)) info.Mtu = ParseLong(v);
                if (kvp.Value.TryGetValue("Speed", out v)) info.Speed = ParseULong(v);
                if (kvp.Value.TryGetValue("AdminStatus", out v)) info.AdminStatus = ParseInt(v);
                if (kvp.Value.TryGetValue("OperStatus", out v)) info.OperStatus = ParseInt(v);
                if (kvp.Value.TryGetValue("InOctets", out v)) info.InOctets = ParseULong(v);
                if (kvp.Value.TryGetValue("OutOctets", out v)) info.OutOctets = ParseULong(v);
                if (kvp.Value.TryGetValue("InErrors", out v)) info.InErrors = ParseUInt(v);
                if (kvp.Value.TryGetValue("OutErrors", out v)) info.OutErrors = ParseUInt(v);
                list.Add(info);
            }

            list = list.OrderBy(i => i.Index).ToList();
            _logger.Info("   Найдено интерфейсов: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить IP адреса.
        /// </summary>
        public List<IpAddressInfo> GetIpAddresses()
        {
            _logger.Info("📡 Сбор IP-адресов...");
            var list = new List<IpAddressInfo>();

            foreach (var kvp in WalkTable("ipAddresses"))
            {
                var info = new IpAddressInfo();
                if (kvp.Value.TryGetValue("Address", out var v)) info.Address = v;
                if (kvp.Value.TryGetValue("Mask", out v)) info.Mask = v;
                if (kvp.Value.TryGetValue("IfIndex", out v)) info.IfIndex = ParseInt(v);

                if (!IsValidIpAddress(info.Address))
                {
                    _logger.Warn("Некорректный IP адрес: {0}, запись пропущена", info.Address);
                    continue;
                }
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
        /// Получить ARP таблицу.
        /// </summary>
        public List<ArpEntry> GetArpTable()
        {
            _logger.Info("🌐 Сбор ARP таблицы...");
            var list = new List<ArpEntry>();

            foreach (var kvp in WalkTable("arpTable"))
            {
                var entry = new ArpEntry();
                if (kvp.Value.TryGetValue("IfIndex", out var v)) entry.IfIndex = ParseInt(v);
                if (kvp.Value.TryGetValue("PhysAddress", out v)) entry.Mac = v;
                if (kvp.Value.TryGetValue("NetAddress", out v)) entry.Ip = v;
                if (kvp.Value.TryGetValue("Type", out v)) entry.Type = ParseInt(v);

                if (!IsValidIpAddress(entry.Ip))
                {
                    _logger.Warn("Некорректный IP адрес в ARP: {0}, запись пропущена", entry.Ip);
                    continue;
                }
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
        /// Получить таблицу маршрутизации.
        /// </summary>
        public List<RouteEntry> GetRoutingTable()
        {
            _logger.Info("🛣️ Сбор таблицы маршрутизации...");
            var list = new List<RouteEntry>();

            foreach (var kvp in WalkTable("routingTable"))
            {
                var entry = new RouteEntry();
                if (kvp.Value.TryGetValue("Dest", out var v)) entry.Dest = v;
                if (kvp.Value.TryGetValue("Mask", out v)) entry.Mask = v;
                if (kvp.Value.TryGetValue("NextHop", out v)) entry.NextHop = v;
                if (kvp.Value.TryGetValue("IfIndex", out v)) entry.IfIndex = ParseInt(v);
                if (kvp.Value.TryGetValue("Type", out v)) entry.Type = ParseInt(v) == 3 ? "direct" : "indirect";
                if (kvp.Value.TryGetValue("Proto", out v))
                {
                    int proto = ParseInt(v);
                    entry.Proto = proto == 2 ? "local" : proto.ToString();
                }
                if (kvp.Value.TryGetValue("Metric", out v)) entry.Metric = ParseInt(v);
                if (kvp.Value.TryGetValue("Age", out v)) entry.Age = ParseInt(v);

                if (!IsValidIpAddress(entry.Dest))
                {
                    _logger.Warn("Некорректный IP адрес назначения: {0}, запись пропущена", entry.Dest);
                    continue;
                }
                if (!IsValidIpAddress(entry.Mask))
                {
                    _logger.Warn("Некорректная маска подсети: {0} для маршрута {1}, запись пропущена", entry.Mask, entry.Dest);
                    continue;
                }
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
        /// Получить информацию о дисках.
        /// </summary>
        public List<DiskInfo> GetStorageInfo()
        {
            _logger.Info("💾 Сбор информации о дисках...");
            var list = new List<DiskInfo>();

            foreach (var kvp in WalkTable("storage"))
            {
                double unitFactor = kvp.Value.TryGetValue("Units", out var v) ? ParseLong(v) : 1024;
                if (unitFactor == 0) unitFactor = 1024;

                double sizeVal = kvp.Value.TryGetValue("Size", out v) ? ParseLong(v) : 0;
                double usedVal = kvp.Value.TryGetValue("Used", out v) ? ParseLong(v) : 0;

                if (sizeVal > 1000 && kvp.Value.TryGetValue("Descr", out var descr))
                {
                    list.Add(new DiskInfo
                    {
                        Description = descr,
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
        /// Получить загрузку CPU.
        /// </summary>
        public List<CpuCore> GetCPULoad()
        {
            _logger.Info("⚡ Сбор данных CPU...");
            var list = new List<CpuCore>();

            int count = 0;
            foreach (var kvp in WalkTable("cpu"))
            {
                int load = kvp.Value.TryGetValue("Load", out var v) ? ParseInt(v) : 0;
                list.Add(new CpuCore { Index = count++, Load = load });
            }

            _logger.Info("   Найдено ядер CPU: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить список процессов.
        /// </summary>
        public List<ProcessInfo> GetProcesses()
        {
            _logger.Info("📋 Сбор списка процессов...");
            var list = new List<ProcessInfo>();

            int count = 0;
            foreach (var kvp in WalkTable("processes"))
            {
                if (count >= SnmpConfig.MaxProcesses) break;

                var proc = new ProcessInfo();
                if (kvp.Value.TryGetValue("Name", out var v)) proc.Name = v;
                if (kvp.Value.TryGetValue("Path", out v)) proc.Path = v;
                if (kvp.Value.TryGetValue("Params", out v)) proc.Params = v;
                if (kvp.Value.TryGetValue("Type", out v))
                {
                    proc.Type = ParseInt(v) == 4 ? "App" : "System";
                }
                if (kvp.Value.TryGetValue("Status", out v))
                {
                    int statusVal = ParseInt(v);
                    proc.Status = (statusVal == 1 || statusVal == 2) ? "Running" : "Other";
                }
                list.Add(proc);
                count++;
            }

            _logger.Info("   Найдено процессов: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить информацию об устройствах.
        /// </summary>
        public List<DeviceInfo> GetDevices()
        {
            _logger.Info("🔧 Сбор информации об устройствах...");
            var list = new List<DeviceInfo>();

            foreach (var kvp in WalkTable("devices"))
            {
                var device = new DeviceInfo();
                if (kvp.Value.TryGetValue("Type", out var v)) device.Type = ParseInt(v);
                if (device.Type <= 0) continue;

                if (kvp.Value.TryGetValue("Descr", out v)) device.Description = v;
                if (kvp.Value.TryGetValue("Status", out v))
                {
                    device.Status = ParseInt(v) == 2 ? "Running" : "Unknown";
                }
                if (kvp.Value.TryGetValue("Errors", out v)) device.Errors = ParseUInt(v);
                list.Add(device);
            }

            _logger.Info("   Найдено устройств: {0}", list.Count);
            return list;
        }

        /// <summary>
        /// Получить статистику протоколов.
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
        /// Получить скалярные значения группы как словарь.
        /// </summary>
        public Dictionary<string, string> GetScalars(string groupId)
        {
            var result = new Dictionary<string, string>();
            try
            {
                var scalarGroup = OidConfigLoader.GetScalarGroup(groupId);

                if (scalarGroup == null)
                {
                    _logger.Warn("Скалярная группа '{0}' не найдена в конфигурации", groupId);
                    return result;
                }

                _logger.Debug("Запрос скалярных значений группы: {0} ({1})", scalarGroup.DisplayName, scalarGroup.Id);

                foreach (var column in scalarGroup.Columns)
                {
                    _logger.Debug("  Запрос скаляра: {0} (OID: {1})", column.Name, column.Oid);
                    result[column.Name] = GetScalar(scalarGroup.Id, column.Name);
                }

                _logger.Debug("Получено {0} скалярных значений", result.Count);
            }
            catch (Exception ex)
            {
                _logger.Warn("Ошибка при запросе скаляров {0}: {1}", groupId, ex.Message);
            }
            return result;
        }

        /// <summary>
        /// Универсальный метод для получения любой таблицы по ключу конфигурации.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> GetUniversalTable(string tableKey)
        {
            return WalkTable(tableKey);
        }

        public void Dispose()
        {
            // Сокеты создаются и освобождаются внутри Messenger (Lextm.SharpSnmpLib);
            // IDisposable оставлен для единообразия API и будущих реализаций.
            GC.SuppressFinalize(this);
        }

        #region Helper Methods

        /// <summary>
        /// Проверка корректности IPv4 адреса.
        /// </summary>
        internal static bool IsValidIpAddress(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;

            string[] parts = ip.Split('.');
            if (parts.Length != 4) return false;

            return parts.All(part => byte.TryParse(part, out _));
        }

        /// <summary>
        /// Проверка корректности MAC адреса.
        /// Поддерживаемые форматы: XX-XX-XX-XX-XX-XX, XX:XX:XX:XX:XX:XX, XXXXXXXXXXXX
        /// </summary>
        internal static bool IsValidMacAddress(string mac)
        {
            if (string.IsNullOrEmpty(mac)) return false;

            return MacAddressRegex().IsMatch(mac);
        }

        [System.Text.RegularExpressions.GeneratedRegex(
            @"^(([0-9A-Fa-f]{2}-){5}[0-9A-Fa-f]{2}|([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}|[0-9A-Fa-f]{12})$")]
        private static partial System.Text.RegularExpressions.Regex MacAddressRegex();

        private static bool IsHex(string input)
        {
            if (input.Length % 2 != 0) return false;
            foreach (char c in input)
                if (!"0123456789ABCDEFabcdef".Contains(c)) return false;
            return true;
        }

        private static int ParseInt(string val) => int.TryParse(val, out int i) ? i : 0;
        private static long ParseLong(string val) => long.TryParse(val, out long l) ? l : 0;
        private static ulong ParseULong(string val) => ulong.TryParse(val, out ulong ul) ? ul : 0;
        private static uint ParseUInt(string val) => uint.TryParse(val, out uint ui) ? ui : 0;

        private static ulong ParseToULong(string rawStr)
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
    /// Модель системной информации.
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
