using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using SnmpMonitor.Config;
using SnmpMonitor.Logging;
using SnmpMonitor.Results;

namespace SnmpMonitor.Services
{
    /// <summary>
    /// Default implementation of SNMP client using SharpSnmpLib
    /// </summary>
    public class UniversalSnmpClient : ISnmpClient
    {
        private readonly string _targetIp;
        private readonly string _community;
        private readonly ILogger _logger;
        private readonly ISnmpDataDecoder _decoder;
        private readonly IValueFormatter _formatter;
        private readonly int _timeout;

        public UniversalSnmpClient(
            string targetIp, 
            string community, 
            ILogger? logger = null,
            ISnmpDataDecoder? decoder = null,
            IValueFormatter? formatter = null,
            int timeout = 5000)
        {
            _targetIp = targetIp;
            _community = community;
            _logger = logger ?? new ConsoleLogger();
            _decoder = decoder ?? new SnmpDataDecoder(_logger);
            _formatter = formatter ?? new ValueFormatter();
            _timeout = timeout;
        }

        /// <summary>
        /// Gets a scalar value by category and name
        /// </summary>
        public Result<string> GetScalar(string category, string name)
        {
            try
            {
                string baseOid = OidConfigLoader.GetScalarOid(category, name);
                
                if (string.IsNullOrEmpty(baseOid))
                {
                    _logger.Error("OID not found for {0}.{1}", category, name);
                    return Result<string>.Failure("OID not found in configuration");
                }
                
                // For scalar values, append .0 to OID if not already present
                string oid = baseOid.EndsWith(".0") ? baseOid : baseOid + ".0";
                
                _logger.Debug("Requesting OID: {0} ({1}.{2})", oid, category, name);
                
                var version = VersionCode.V2;
                var endPoint = new IPEndPoint(IPAddress.Parse(_targetIp), 161);
                var communityParam = new OctetString(_community);
                var oidList = new List<Variable> { new Variable(new ObjectIdentifier(oid)) };
                
                Messenger.Get(version, endPoint, communityParam, oidList, _timeout);
                
                if (oidList.Count > 0 && oidList[0].Data != null)
                {
                    var variable = oidList[0];
                    
                    // Check for SNMP error responses
                    if (variable.Data is NoSuchInstance ||
                        variable.Data is NoSuchObject ||
                        variable.Data is EndOfMibView)
                    {
                        _logger.Warn("SNMP responded that OID is unavailable: {0}", oid);
                        return Result<string>.Failure("OID not available on device");
                    }
                    
                    string value = _decoder.Decode(variable.Data);
                    _logger.Debug("Received: {0} = {1}", oid, value);
                    return Result<string>.Success(value);
                }
                
                _logger.Warn("Empty response for OID: {0}. Data type: {1}", oid, oidList[0].Data?.GetType().Name ?? "null");
                return Result<string>.Failure("No data received");
            }
            catch (Exception ex)
            {
                _logger.Error("Error requesting {0}.{1}: {2}", category, name, ex);
                return Result<string>.Failure(ex);
            }
        }

        /// <summary>
        /// Walks a table and returns data as dictionary
        /// </summary>
        public Result<Dictionary<string, Dictionary<string, string>>> WalkTable(string tableKey)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            
            try
            {
                TableConfig tableConfig = OidConfigLoader.GetTableConfig(tableKey);
                _logger.Debug("Walking table: {0} (OID: {1})", tableConfig.Name, tableConfig.BaseOid);
                
                // Load common value mapping for table if specified
                Dictionary<string, string>? tableValueMap = null;
                if (!string.IsNullOrEmpty(tableConfig.ValueMapping))
                {
                    tableValueMap = OidConfigLoader.LoadValueMapping(tableConfig.ValueMapping);
                }
                
                // Collect data for each field
                var fieldData = new Dictionary<string, Dictionary<string, string>>();
                
                foreach (var field in tableConfig.Fields)
                {
                    // Load field-specific value mapping if specified (overrides table mapping)
                    Dictionary<string, string>? fieldValueMap = null;
                    if (!string.IsNullOrEmpty(field.ValueMapping))
                    {
                        fieldValueMap = OidConfigLoader.LoadValueMapping(field.ValueMapping);
                    }
                    
                    var walkResult = WalkSingleField(
                        field.Oid, 
                        field.Type, 
                        field.Format, 
                        field.Map, 
                        fieldValueMap ?? tableValueMap);
                    
                    fieldData[field.Name] = walkResult;
                }
                
                // Determine indexes (combine all keys)
                var allIndexes = new HashSet<string>();
                foreach (var field in fieldData.Values)
                {
                    foreach (var key in field.Keys)
                    {
                        allIndexes.Add(key);
                    }
                }
                
                // Build result by indexes
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
                
                _logger.Debug("Walk {0}: received {1} records", tableConfig.Name, result.Count);
                return Result<Dictionary<string, Dictionary<string, string>>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.Error("Error walking table {0}: {1}", tableKey, ex);
                return Result<Dictionary<string, Dictionary<string, string>>>.Failure(ex);
            }
        }

        /// <summary>
        /// Walk a single field of a table
        /// </summary>
        private Dictionary<string, string> WalkSingleField(
            string rootOid, 
            string? fieldType = null, 
            string? format = null, 
            Dictionary<string, string>? map = null, 
            Dictionary<string, string>? valueMapping = null)
        {
            var result = new Dictionary<string, string>();
            
            try
            {
                var version = VersionCode.V2;
                var endPoint = new IPEndPoint(IPAddress.Parse(_targetIp), 161);
                var communityParam = new OctetString(_community);
                var rootOidObj = new ObjectIdentifier(rootOid);
                
                var variables = new List<Variable>();
                Messenger.Walk(version, endPoint, communityParam, rootOidObj, variables, _timeout, WalkMode.WithinSubtree);
                
                if (variables.Count == 0) return result;

                foreach (var variable in variables)
                {
                    string fullOid = variable.Id.ToString();
                    string index = fullOid.Substring(rootOid.Length);
                    if (index.StartsWith(".")) index = index.Substring(1);
                    
                    string value = DecodeFieldValue(variable, index, fieldType, format, map, valueMapping);
                    result[index] = value;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("Error walking {0}: {1}", rootOid, ex.Message);
            }
            
            return result;
        }

        /// <summary>
        /// Decode a field value based on its type
        /// </summary>
        private string DecodeFieldValue(
            Variable variable,
            string index,
            string? fieldType,
            string? format,
            Dictionary<string, string>? map,
            Dictionary<string, string>? valueMapping)
        {
            // Handle 'index' type - value comes from OID index
            if (fieldType == "index")
                return index;

            // Handle 'ipaddr' type
            if (fieldType == "ipaddr")
            {

                // Validate IP format
                if (IsValidIpAddress(_decoder.DecodeIndexToIpAddress(index)))
                    return _decoder.DecodeIndexToIpAddress(index);
                
                // Try to get bytes from OctetString
                if (variable.Data is OctetString octetStr)
                {
                    byte[] bytes = octetStr.ToBytes();
                    if (bytes.Length == 4)
                        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
                }
                
                return _decoder.Decode(variable.Data);
            }

            // Handle 'macaddress' type
            if (fieldType == "macaddress")
            {
                if (variable.Data is OctetString macOctetStr)
                {
                    byte[] bytes = macOctetStr.ToBytes();
                    if (bytes.Length >= 6)
                        return _decoder.DecodeMacAddress(bytes);
                }
                return _decoder.Decode(variable.Data);
            }

            // Handle numeric types with formatting or valueMapping
            if (fieldType is "long" or "int" or "uint" or "ulong")
            {
                string? numericValue = ExtractNumericValue(variable.Data);
                
                if (!string.IsNullOrEmpty(format))
                {
                    return !string.IsNullOrEmpty(numericValue) 
                        ? _formatter.Format(numericValue, format) 
                        : "N/A";
                }
                
                if (valueMapping != null && !string.IsNullOrEmpty(numericValue))
                {
                    return _formatter.ApplyMapping(numericValue, valueMapping);
                }
                
                return numericValue ?? "N/A";
            }

            // Handle 'oid' type with valueMapping - use decoder to get the raw OID string
            if (fieldType == "oid")
            {
                string rawValue = _decoder.Decode(variable.Data);
                
                if (valueMapping != null)
                {
                    return _formatter.ApplyMapping(rawValue, valueMapping);
                }
                
                return rawValue;
            }

            // Default: decode and apply mappings
            string decodedValue = _decoder.Decode(variable.Data);
            
            if (valueMapping != null && valueMapping.TryGetValue(decodedValue, out var mappedValue))
                return mappedValue;
            
            if (map != null && map.TryGetValue(decodedValue, out var inlineMappedValue))
                return inlineMappedValue;
            
            return decodedValue;
        }

        /// <summary>
        /// Extract numeric value from SNMP data
        /// </summary>
        private static string? ExtractNumericValue(ISnmpData data)
        {
            if (data is Gauge32 gauge32) return gauge32.ToUInt32().ToString();
            if (data is Integer32 asnInt) return asnInt.ToInt32().ToString();
            if (data is Counter32 counter32) return counter32.ToUInt32().ToString();
            if (data is Counter64 counter64) return counter64.ToUInt64().ToString();
            
            // Fallback: try to parse string representation
            string rawValue = data.ToString();
            if (ulong.TryParse(rawValue, out ulong parsedValue))
                return parsedValue.ToString();
            
            return null;
        }

        /// <summary>
        /// Validate IPv4 address
        /// </summary>
        private static bool IsValidIpAddress(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;

            string[] parts = ip.Split('.');
            if (parts.Length != 4) return false;

            return parts.All(part => byte.TryParse(part, out _));
        }
    }
}
