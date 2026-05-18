# SnmpMonitor - SNMP Monitoring Tool

## Overview
A cross-platform SNMP monitoring tool for collecting and exporting network device data to CSV and PDF formats.

## Features
- **Universal SNMP Manager**: Configurable OID-based data collection
- **Multiple Export Formats**: CSV and PDF reports
- **Configurable Tables**: Define custom SNMP tables via JSON configuration
- **Value Mapping**: Automatic conversion of numeric codes to human-readable values
- **Type Support**: Handles various SNMP data types (Integer, Counter32/64, Gauge32, OctetString)
- **Special Decoding**: Built-in support for IP addresses, MAC addresses, and speed formatting

## Project Structure

```
SnmpMonitor/
├── Program.cs                      # Application entry point
├── Config/
│   ├── SnmpConfig.cs               # Application settings
│   ├── OidConfig.cs                # OID configuration loader
│   ├── snmp-tables.json            # Table definitions
│   └── oid-mappings.json           # Value mappings
├── Logging/
│   ├── ILogger.cs                  # Logger interface
│   ├── ConsoleLogger.cs            # Console output
│   ├── FileLogger.cs               # File output
│   └── CompositeLogger.cs          # Multi-target logging
├── Models/
│   └── DataModels.cs               # Data transfer objects
├── Services/                       # [NEW] Service layer interfaces and implementations
│   ├── ISnmpServices.cs            # Service interfaces
│   ├── SnmpDataDecoder.cs          # SNMP data decoding
│   ├── ValueFormatter.cs           # Value formatting logic
│   └── UniversalSnmpClient.cs      # Refactored SNMP client
├── Reporting/
│   ├── UniversalReportGenerator.cs # Report orchestration
│   ├── UniversalCsvReporter.cs     # CSV export
│   └── UniversalPdfReporter.cs     # PDF export
├── Validation/
│   └── TableDataValidator.cs       # Data validation utilities
└── Results/                        # [NEW] Result pattern types
    └── Result.cs                   # Result<T> for error handling
```

## Requirements
- .NET 8.0 SDK
- SNMP v2c enabled on target devices

## Installation

```bash
dotnet restore
dotnet build
```

## Usage

### Basic Execution
```bash
dotnet run -- <target-ip>
# Example:
dotnet run -- 192.168.1.1
```

### Configuration
Edit `appsettings.json` to customize:
- Target IP address
- SNMP community string
- Timeout and retry settings
- Output paths
- Log level

### Custom Tables
Add new tables in `Config/snmp-tables.json`:
```json
{
  "tables": {
    "myCustomTable": {
      "name": "My Table",
      "description": "Custom table description",
      "baseOid": ".1.3.6.1.2.1.X",
      "fields": [
        { "name": "ColumnName", "oid": ".1.3.6.1.2.1.X.Y", "type": "string" }
      ]
    }
  }
}
```

### Supported Field Types
- `string` - Text values
- `int`, `long`, `uint`, `ulong` - Numeric values
- `ipaddr` - IPv4 addresses
- `macaddress` - MAC addresses
- `index` - OID index values
- `oid` - Object identifiers

### Formatting Options
- `speed_mbps` - Convert Mbps to human-readable format (e.g., "1000 Mb/s" → "1.0 Gb/s")

## Architecture Improvements

### Result Pattern
The codebase now uses a `Result<T>` pattern for explicit error handling instead of returning magic strings like "No Data":

```csharp
Result<string> result = snmpClient.GetScalar("system", "SysName");
if (result.IsSuccess)
{
    Console.WriteLine($"Device name: {result.Value}");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

### Service Layer
SNMP operations are now separated into distinct services:
- **ISnmpClient**: Abstracts SNMP communication
- **ISnmpDataDecoder**: Handles ASN.1 type decoding
- **IValueFormatter**: Applies formatting rules

This enables:
- Better testability through dependency injection
- Single Responsibility Principle compliance
- Easier maintenance and extension

## License
MIT License
