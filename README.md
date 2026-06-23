# SnmpMonitor - Универсальный SNMP Мониторинг

## Обзор
Кроссплатформенный инструмент SNMP-мониторинга для сбора и экспорта данных сетевых устройств в форматы CSV и PDF. Приложение полностью конфигурируемо через JSON-файлы без необходимости изменения кода.

## Возможности
- **Универсальный SNMP менеджер**: Сбор данных на основе OID из конфигурационных файлов
- **Множественные форматы экспорта**: CSV и PDF отчеты
- **Конфигурируемые таблицы**: Определение SNMP-таблиц через JSON
- **Маппинг значений**: Автоматическое преобразование числовых кодов в читаемые значения
- **Поддержка типов**: Работа с различными SNMP-типами (Integer, Counter32/64, Gauge32, OctetString, ObjectIdentifier)
- **Специальное декодирование**: Встроенная поддержка IP-адресов, MAC-адресов, форматирования скорости
- **Обработка ошибок**: Pattern Result<T> для явной обработки ошибок
- **Валидация данных**: Проверка корректности IP и MAC адресов

## Архитектура конфигурации

Приложение использует три основных конфигурационных файла:

### 1. `Config/snmp-tables.json` - Определения таблиц и скаляров
Содержит:
- Секцию `tables` - определения табличных данных с полями и OID
- Секцию `scalars` - определения скалярных значений по категориям

### 2. `Config/oid-mappings.json` - Справочник маппинга значений
Содержит секцию `mappings` с расшифровками:
- Числовых кодов в текстовые значения (например, ifType: 6 → "ethernetCsmacd")
- OID в текстовые описания (например, hrStorageType OID → "Disk storage")

### 3. `appsettings.json` - Настройки приложения
Содержит:
- SNMP настройки (целевой IP, сообщество, таймаут)
- Настройки логирования
- Настройки экспорта (пути, форматы)

## Структура проекта

```
SnmpMonitor/
├── Program.cs                          # Точка входа приложения
├── appsettings.json                    # Основные настройки приложения
├── SnmpMonitor.csproj                  # Проект .NET 8.0
├── SnmpMonitor.slnx                    # Решение Visual Studio
│
├── Config/                             # Конфигурация
│   ├── SnmpConfig.cs                   # Загрузчик настроек приложения
│   ├── OidConfig.cs                    # Загрузчик OID конфигурации
│   ├── snmp-tables.json                # Определения таблиц и скаляров
│   └── oid-mappings.json               # Справочник маппинга значений
│
├── Logging/                            # Система логирования
│   ├── ILogger.cs                      # Интерфейс логгера
│   ├── ConsoleLogger.cs                # Логгер в консоль
│   ├── FileLogger.cs                   # Логгер в файл
│   └── CompositeLogger.cs              # Композитный логгер
│
├── Models/                             # Модели данных
│   └── DataModels.cs                   # DTO для результатов SNMP
│
├── Services/                           # Сервисный слой
│   ├── ISnmpServices.cs                # Интерфейсы сервисов
│   ├── SnmpDataDecoder.cs              # Декодер SNMP данных
│   ├── ValueFormatter.cs               # Форматировщик значений
│   └── UniversalSnmpClient.cs          # SNMP клиент
│
├── Snmp/                               # SNMP менеджер
│   └── UniversalSnmpManager.cs         # Универсальный менеджер SNMP
│
├── Reporting/                          # Генерация отчетов
│   ├── UniversalReportGenerator.cs     # Оркестратор отчетов
│   ├── UniversalCsvReporter.cs         # Экспорт в CSV
│   └── UniversalPdfReporter.cs         # Экспорт в PDF
│
├── Validation/                         # Валидация данных
│   └── TableDataValidator.cs           # Валидатор таблиц SNMP
│
└── Results/                            # Pattern Result
    └── Result.cs                       # Result<T> для обработки ошибок
```

## Требования
- .NET 8.0 SDK
- SNMP v2c включен на целевых устройствах
- Пакеты NuGet:
  - Lextm.SharpSnmpLib (версия 12.5.7)
  - Newtonsoft.Json (версия 13.0.4)
  - QuestPDF (версия 2026.5.0)

## Установка

```bash
dotnet restore
dotnet build
```

## Использование

### Базовый запуск
```bash
dotnet run -- <target-ip>
# Пример:
dotnet run -- 192.168.1.1
```

### Конфигурация
Отредактируйте `appsettings.json` для настройки:
- Целевого IP адреса
- SNMP сообщества
- Таймаута и количества попыток
- Путей вывода
- Уровня логирования

### Формат конфигурации таблиц

#### Структура snmp-tables.json

```json
{
  "tables": {
    "<ключ_таблицы>": {
      "name": "<Отображаемое имя>",
      "description": "<Описание таблицы>",
      "baseOid": "<Базовый OID таблицы>",
      "valueMapping": "<Имя секции маппинга из oid-mappings.json>",
      "fields": [
        {
          "name": "<Имя поля>",
          "oid": "<Полный OID поля>",
          "type": "<Тип данных>",
          "format": "<Формат (опционально)>",
          "valueMapping": "<Индивидуальный маппинг (опционально)>"
        }
      ]
    }
  },
  "scalars": {
    "<категория>": {
      "<имя_параметра>": "<OID.с.суффиксом.0>"
    }
  }
}
```

#### Поддерживаемые типы полей

| Тип | Описание | Пример использования |
|-----|----------|---------------------|
| `string` | Текстовые значения | Описание интерфейса, имя процесса |
| `int` | Целое число со знаком | Статус, тип устройства |
| `long` | Длинное целое со знаком | MTU, размер в единицах |
| `uint` | Целое число без знака | Счетчики ошибок |
| `ulong` | Длинное целое без знака | Октеты, скорость |
| `ipaddr` | IPv4 адрес | Адрес назначения, маска подсети |
| `macaddress` | MAC адрес | Физический адрес ARP |
| `index` | Индекс OID | Ключ записи таблицы |
| `oid` | Object Identifier | Тип хранилища, тип устройства |

#### Форматирование значений

| Формат | Описание | Пример |
|--------|----------|--------|
| `speed_mbps` | Конвертация Mbps в человекочитаемый формат | 1000 → "1.0 Gb/s" |

#### Маппинг значений

Поля могут использовать `valueMapping` для преобразования числовых кодов или OID в текстовые описания. Имя маппинга должно соответствовать секции в `oid-mappings.json`.

Пример конфигурации поля с маппингом:
```json
{
  "name": "Type",
  "oid": ".1.3.6.1.2.1.2.2.1.3",
  "type": "long",
  "valueMapping": "ifType"
}
```

### Пример добавления новой таблицы

Добавьте новую таблицу в `Config/snmp-tables.json`:

```json
{
  "tables": {
    "newTable": {
      "name": "New Table",
      "description": "Описание новой таблицы",
      "baseOid": ".1.3.6.1.2.1.X",
      "fields": [
        { "name": "Column1", "oid": ".1.3.6.1.2.1.X.Y.1", "type": "string" },
        { "name": "Column2", "oid": ".1.3.6.1.2.1.X.Y.2", "type": "int", "valueMapping": "myMapping" }
      ]
    }
  }
}
```

И добавьте соответствующий маппинг в `Config/oid-mappings.json`:

```json
{
  "mappings": {
    "myMapping": {
      "description": "Описание справочника",
      "type": "int",
      "values": {
        "1": "Value One",
        "2": "Value Two"
      }
    }
  }
}
```

### Пример добавления новых скаляров

Добавьте новую категорию скаляров в `Config/snmp-tables.json`:

```json
{
  "scalars": {
    "newCategory": {
      "Param1": ".1.3.6.1.2.1.X.1.0",
      "Param2": ".1.3.6.1.2.1.X.2.0"
    }
  }
}
```

## Архитектурные особенности

### Pattern Result<T>
Код использует паттерн `Result<T>` для явной обработки ошибок вместо магических строк:

```csharp
Result<string> result = snmpClient.GetScalar("system", "SysName");
if (result.IsSuccess)
{
    Console.WriteLine($"Имя устройства: {result.Value}");
}
else
{
    Console.WriteLine($"Ошибка: {result.Error}");
}
```

### Сервисный слой
SNMP операции разделены на отдельные сервисы:
- **ISnmpClient**: Абстракция SNMP коммуникации
- **ISnmpDataDecoder**: Декодирование ASN.1 типов
- **IValueFormatter**: Применение правил форматирования

Преимущества:
- Лучшая тестируемость через dependency injection
- Соблюдение принципа единственной ответственности
- Простота поддержки и расширения

### Обработка SNMP ошибок
Система корректно обрабатывает SNMP ответы об отсутствии объектов:
- `NoSuchInstance` - OID не существует на устройстве
- `NoSuchObject` - OID недоступен
- `EndOfMibView` - Достигнут конец MIB дерева

### Декодирование IP адресов
Поддерживаются различные форматы представления IP адресов в SNMP:
- Числовой формат в индексе OID (192.168.1.1)
- Бинарный формат (байты как символы)
- UTF-8 encoded bytes

## Предопределенные таблицы

Проект включает конфигурации для следующих таблиц:

| Таблица | Описание | MIB |
|---------|----------|-----|
| `interfaces` | Сетевые интерфейсы | IF-MIB, IF-X-MIB |
| `ipAddresses` | IP адреса | IP-MIB |
| `arpTable` | ARP таблица | IP-MIB |
| `routingTable` | Таблица маршрутизации | IP-MIB |
| `storage` | Дисковое пространство | HOST-RESOURCES-MIB |
| `cpu` | Загрузка CPU | HOST-RESOURCES-MIB |
| `processes` | Список процессов | HOST-RESOURCES-MIB |
| `devices` | Устройства системы | HOST-RESOURCES-MIB |
| `temperature` | Датчики температуры | UCD-SNMP-MIB |

## Предопределенные скаляры

| Категория | Параметры | MIB |
|-----------|-----------|-----|
| `system` | SysDescr, SysUpTime, SysContact, SysName, SysLocation | SNMPv2-MIB |
| `ipStats` | IpForwarding, IpInReceives, IpOutRequests | IP-MIB |
| `tcpStats` | TcpMaxConn, TcpInSegs, TcpOutSegs | TCP-MIB |
| `udpStats` | UdpInDatagrams, UdpOutDatagrams | UDP-MIB |
| `icmpStats` | IcmpInMsgs, IcmpOutMsgs, IcmpInEchos, IcmpOutEchos | ICMP-MIB |
| `snmpStats` | SnmpInPkts, SnmpOutPkts | SNMPv2-MIB |

## Логи работы

Логи записываются в:
- Консоль (цветной вывод)
- Файл `logs/poller.log` (настраивается в appsettings.json)

Уровни логирования: Debug, Information, Warn, Error

## Лицензия
MIT License
