using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using SnmpMonitor.Logging;

namespace SnmpMonitor.Services
{
    /// <summary>
    /// Default implementation of SNMP data decoder
    /// </summary>
    public class SnmpDataDecoder : ISnmpDataDecoder
    {
        private readonly ILogger _logger;

        public SnmpDataDecoder(ILogger? logger = null)
        {
            _logger = logger ?? new ConsoleLogger();
        }

        /// <summary>
        /// Decodes raw SNMP data to string representation
        /// </summary>
        public string Decode(ISnmpData asnValue)
        {
            if (asnValue == null) return string.Empty;

            // Handle ObjectIdentifier (OID type) - return the OID as string without any transformation
            if (asnValue is ObjectIdentifier oid)
                return oid.ToString();

            // Handle Integer/Integer32
            if (asnValue is Integer32 asnInt)
                return asnInt.ToInt32().ToString();

            // Handle Counter32
            if (asnValue is Counter32 counter32)
                return counter32.ToUInt32().ToString();

            // Handle Counter64 for large numbers
            if (asnValue is Counter64 counter64)
                return counter64.ToUInt64().ToString();

            // Handle Gauge32
            if (asnValue is Gauge32 gauge32)
                return gauge32.ToUInt32().ToString();

            // Handle OctetString - may contain IP address in binary format or text
            if (asnValue is OctetString octetStr)
            {
                try
                {
                    byte[] bytes = octetStr.ToBytes();

                    // Check if this is an IP address (4 bytes)
                    if (bytes.Length == 4)
                        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";

                    if (bytes != null && bytes.Length > 0)
                    {
                        string utf8Str = System.Text.Encoding.UTF8.GetString(bytes);

                        // Clean from non-printable characters and control codes
                        var cleanChars = new List<char>();
                        foreach (char c in utf8Str)
                        {
                            if (!char.IsControl(c) && 
                                !(char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format))
                            {
                                cleanChars.Add(c);
                            }
                        }

                        string cleanedStr = new string(cleanChars.ToArray()).Trim();

                        // If string contains at least one readable character, return it
                        if (cleanedStr.Any(c => c >= 32 && c < 127) || 
                            cleanedStr.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c)))
                            return cleanedStr;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug("Error decoding OctetString: {0}", ex.Message);
                }
            }

            // Return string representation for other types
            return asnValue.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Decodes an index OID to IP address
        /// Supports multiple formats: numeric, binary, UTF-8 encoded bytes
        /// </summary>
        public string DecodeIndexToIpAddress(string index)
        {
            if (string.IsNullOrEmpty(index)) return index;

            // Remove leading dot if present
            if (index.StartsWith("."))
                index = index.Substring(1);

            // Format 1: Numeric format with dots (e.g., "192.168.1.1")
            if (index.Contains("."))
            {
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
                            return $"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]}";
                    }
                    catch { }
                }
            }

            // Format 2: Binary format - each character represents a byte
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
                    if (allValid)
                        return $"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]}";
                }
                catch { }
            }

            // Format 3: UTF-8 encoded bytes
            try
            {
                byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(index);
                if (utf8Bytes.Length >= 4)
                    return $"{utf8Bytes[0]}.{utf8Bytes[1]}.{utf8Bytes[2]}.{utf8Bytes[3]}";
            }
            catch { }

            return index;
        }

        /// <summary>
        /// Decodes MAC address from bytes
        /// </summary>
        public string DecodeMacAddress(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 6)
                return string.Empty;

            return $"{bytes[0]:X2}-{bytes[1]:X2}-{bytes[2]:X2}-{bytes[3]:X2}-{bytes[4]:X2}-{bytes[5]:X2}";
        }
    }
}
