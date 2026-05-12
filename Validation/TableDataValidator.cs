using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SnmpMonitor.Models;

namespace SnmpMonitor.Validation
{
    /// <summary>
    /// Валидатор данных SNMP таблиц
    /// </summary>
    public class TableDataValidator
    {
        private readonly List<ValidationResult> _errors = new();
        private readonly List<ValidationResult> _warnings = new();

        /// <summary>
        /// Результаты валидации
        /// </summary>
        public IReadOnlyList<ValidationResult> Errors => _errors.AsReadOnly();
        public IReadOnlyList<ValidationResult> Warnings => _warnings.AsReadOnly();

        /// <summary>
        /// Валидировать ARP таблицу
        /// </summary>
        public void ValidateArpTable(IReadOnlyList<ArpEntry> entries, string tableName = "ARP Table")
        {
            foreach (var entry in entries)
            {
                // Валидация IP адреса
                if (!IsValidIpAddress(entry.Ip))
                {
                    _errors.Add(new ValidationResult(
                        tableName,
                        $"Invalid IP address: {entry.Ip}",
                        ValidationSeverity.Error));
                }

                // Валидация MAC адреса
                if (!IsValidMacAddress(entry.Mac))
                {
                    _errors.Add(new ValidationResult(
                        tableName,
                        $"Invalid MAC address: {entry.Mac}",
                        ValidationSeverity.Error));
                }

                // Валидация IfIndex (должен быть положительным)
                if (entry.IfIndex <= 0)
                {
                    _warnings.Add(new ValidationResult(
                        tableName,
                        $"Invalid IfIndex: {entry.IfIndex}",
                        ValidationSeverity.Warning));
                }

                // Валидация типа ARP записи
                if (entry.Type < 1 || entry.Type > 4)
                {
                    _warnings.Add(new ValidationResult(
                        tableName,
                        $"Unknown ARP type: {entry.Type}",
                        ValidationSeverity.Warning));
                }
            }
        }

        /// <summary>
        /// Валидировать таблицу маршрутизации
        /// </summary>
        public void ValidateRoutingTable(IReadOnlyList<RouteEntry> entries, string tableName = "Routing Table")
        {
            foreach (var entry in entries)
            {
                // Валидация IP адреса назначения
                if (!IsValidIpAddress(entry.Dest))
                {
                    _errors.Add(new ValidationResult(
                        tableName,
                        $"Invalid destination IP address: {entry.Dest}",
                        ValidationSeverity.Error));
                }

                // Валидация маски подсети
                if (!IsValidIpAddress(entry.Mask))
                {
                    _errors.Add(new ValidationResult(
                        tableName,
                        $"Invalid subnet mask: {entry.Mask}",
                        ValidationSeverity.Error));
                }

                // Валидация IP адреса следующего хопа
                if (!string.IsNullOrEmpty(entry.NextHop) && !IsValidIpAddress(entry.NextHop))
                {
                    _errors.Add(new ValidationResult(
                        tableName,
                        $"Invalid next hop IP address: {entry.NextHop}",
                        ValidationSeverity.Error));
                }

                // Валидация IfIndex
                if (entry.IfIndex <= 0)
                {
                    _warnings.Add(new ValidationResult(
                        tableName,
                        $"Invalid IfIndex: {entry.IfIndex}",
                        ValidationSeverity.Warning));
                }

                // Валидация метрики
                if (entry.Metric < 0)
                {
                    _warnings.Add(new ValidationResult(
                        tableName,
                        $"Negative metric: {entry.Metric}",
                        ValidationSeverity.Warning));
                }
            }
        }

        /// <summary>
        /// Валидировать таблицу IP адресов
        /// </summary>
        public void ValidateIpAddresses(IReadOnlyList<IpAddressInfo> entries, string tableName = "IP Addresses")
        {
            foreach (var entry in entries)
            {
                // Валидация IP адреса
                if (!IsValidIpAddress(entry.Address))
                {
                    _errors.Add(new ValidationResult(
                        tableName,
                        $"Invalid IP address: {entry.Address}",
                        ValidationSeverity.Error));
                }

                // Валидация маски подсети
                if (!IsValidIpAddress(entry.Mask))
                {
                    _errors.Add(new ValidationResult(
                        tableName,
                        $"Invalid subnet mask: {entry.Mask}",
                        ValidationSeverity.Error));
                }

                // Валидация IfIndex
                if (entry.IfIndex <= 0)
                {
                    _warnings.Add(new ValidationResult(
                        tableName,
                        $"Invalid IfIndex: {entry.IfIndex}",
                        ValidationSeverity.Warning));
                }
            }
        }

        /// <summary>
        /// Проверка корректности IPv4 адреса
        /// </summary>
        private static bool IsValidIpAddress(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;

            // Проверяем формат с помощью регулярного выражения
            var pattern = @"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$";
            var match = Regex.Match(ip, pattern);

            if (!match.Success) return false;

            // Проверяем каждый октет на диапазон 0-255
            for (int i = 1; i <= 4; i++)
            {
                if (!byte.TryParse(match.Groups[i].Value, out _))
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
            var pattern1 = @"^([0-9A-Fa-f]{2}-){5}[0-9A-Fa-f]{2}$";
            if (Regex.IsMatch(mac, pattern1)) return true;

            // Формат с двоеточиями: XX:XX:XX:XX:XX:XX
            var pattern2 = @"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$";
            if (Regex.IsMatch(mac, pattern2)) return true;

            // Формат без разделителей: XXXXXXXXXXXX
            var pattern3 = @"^[0-9A-Fa-f]{12}$";
            if (Regex.IsMatch(mac, pattern3)) return true;

            return false;
        }

        /// <summary>
        /// Очистить результаты валидации
        /// </summary>
        public void Clear()
        {
            _errors.Clear();
            _warnings.Clear();
        }

        /// <summary>
        /// Есть ли ошибки валидации
        /// </summary>
        public bool HasErrors => _errors.Count > 0;

        /// <summary>
        /// Получить сводку результатов валидации
        /// </summary>
        public string GetSummary()
        {
            return $"Validation complete: {_errors.Count} errors, {_warnings.Count} warnings";
        }
    }

    /// <summary>
    /// Результат валидации
    /// </summary>
    public class ValidationResult
    {
        public string TableName { get; }
        public string Message { get; }
        public ValidationSeverity Severity { get; }

        public ValidationResult(string tableName, string message, ValidationSeverity severity)
        {
            TableName = tableName;
            Message = message;
            Severity = severity;
        }

        public override string ToString()
        {
            return $"[{Severity}] {TableName}: {Message}";
        }
    }

    /// <summary>
    /// Уровень серьезности результата валидации
    /// </summary>
    public enum ValidationSeverity
    {
        Error,
        Warning,
        Info
    }
}
