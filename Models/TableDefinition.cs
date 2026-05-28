using System.Text.Json.Serialization;

namespace SnmpMonitor.Models
{
    /// <summary>
    /// Тип элемента данных в таблице
    /// </summary>
    public enum ColumnType
    {
        Scalar, // Одиночное значение (требует добавления .0 к OID при запросе Get)
        Column, // Колонка табличного объекта (требует обхода GetBulk/Walk)
        Index   // Поле индекса (вычисляется из суффикса OID)
    }

    /// <summary>
    /// Описание колонки в определении таблицы
    /// </summary>
    public class ColumnDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("oid")]
        public string Oid { get; set; } = string.Empty; // Базовый OID (без индекса и без .0 для скаляров)

        [JsonPropertyName("type")]
        public ColumnType Type { get; set; } = ColumnType.Column;

        [JsonPropertyName("mapping")]
        public string? MappingKey { get; set; } // Ключ для поиска в oid-mappings.json

        [JsonPropertyName("format")]
        public string? Format { get; set; } // Форматирование (например, IpAddress, MacAddress)
    }

    /// <summary>
    /// Определение таблицы
    /// </summary>
    public class TableDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        // Если true, используется GetBulk для получения всех строк. 
        // Если false (или для скаляров), используются точечные Get запросы или логика скаляров.
        [JsonPropertyName("isTable")]
        public bool IsTable { get; set; } = true;

        // OID входа в таблицу (для Walk). Для скалярных групп может быть пустым или корневым.
        [JsonPropertyName("rootOid")]
        public string? RootOid { get; set; }

        [JsonPropertyName("columns")]
        public List<ColumnDefinition> Columns { get; set; } = new();
        
        // Правила формирования индекса (если сложные случаи)
        [JsonPropertyName("indexStrategy")]
        public string? IndexStrategy { get; set; } // "simple", "composite", "ip"
    }
}
