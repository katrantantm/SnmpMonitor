using System;

namespace SnmpMonitor.Services
{
    /// <summary>
    /// Interface for formatting SNMP values based on configuration
    /// </summary>
    public interface IValueFormatter
    {
        /// <summary>
        /// Formats a raw value according to the specified format type
        /// </summary>
        string Format(string rawValue, string? format);

        /// <summary>
        /// Applies value mapping to a raw value
        /// </summary>
        string ApplyMapping(string? rawValue, System.Collections.Generic.Dictionary<string, string>? mapping);
    }

    /// <summary>
    /// Interface for decoding SNMP data types
    /// </summary>
    public interface ISnmpDataDecoder
    {
        /// <summary>
        /// Decodes raw SNMP data to string representation
        /// </summary>
        string Decode(Lextm.SharpSnmpLib.ISnmpData data);

        /// <summary>
        /// Decodes an index OID to IP address
        /// </summary>
        string DecodeIndexToIpAddress(string index);

        /// <summary>
        /// Decodes MAC address from bytes
        /// </summary>
        string DecodeMacAddress(byte[] bytes);
    }

    /// <summary>
    /// Interface for SNMP operations
    /// </summary>
    public interface ISnmpClient
    {
        /// <summary>
        /// Gets a scalar value by category and name
        /// </summary>
        Results.Result<string> GetScalar(string category, string name);

        /// <summary>
        /// Walks a table and returns data as dictionary
        /// </summary>
        Results.Result<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>> WalkTable(string tableKey);
    }
}
