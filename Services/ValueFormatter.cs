using System;
using System.Collections.Generic;
using SnmpMonitor.Config;

namespace SnmpMonitor.Services
{
    /// <summary>
    /// Default implementation of value formatter for SNMP data
    /// </summary>
    public class ValueFormatter : IValueFormatter
    {
        /// <summary>
        /// Formats a raw value according to the specified format type
        /// </summary>
        public string Format(string rawValue, string? format)
        {
            if (string.IsNullOrEmpty(rawValue)) return rawValue;
            if (string.IsNullOrEmpty(format)) return rawValue;

            switch (format.ToLowerInvariant())
            {
                case "speed_mbps":
                    return FormatSpeed(rawValue);
                default:
                    return rawValue;
            }
        }

        /// <summary>
        /// Applies value mapping to a raw value
        /// </summary>
        public string ApplyMapping(string? rawValue, Dictionary<string, string>? mapping)
        {
            if (string.IsNullOrEmpty(rawValue)) return string.Empty;
            if (mapping == null || mapping.Count == 0) return rawValue!;
            
            // Normalize OID by removing leading dot
            string normalizedKey = rawValue!.TrimStart('.');
            
            // Try exact match first (normalized key without leading dot)
            if (mapping.TryGetValue(normalizedKey, out var mappedValue))
                return mappedValue;
            
            // Try with leading dot as alternative
            string altKey = "." + normalizedKey;
            if (mapping.TryGetValue(altKey, out mappedValue))
                return mappedValue;
            
            // For numeric types, also try direct string comparison
            if (mapping.TryGetValue(rawValue!, out mappedValue))
                return mappedValue;
                
            return rawValue!;
        }

        private static string FormatSpeed(string value)
        {
            if (!ulong.TryParse(value, out ulong speedMbps))
                return "N/A";

            if (speedMbps == 0)
                return "0 bps";
            
            if (speedMbps >= 1_000)
                return $"{speedMbps / 1_000.0:F1} Gb/s";
            
            return $"{speedMbps} Mb/s";
        }
    }
}
