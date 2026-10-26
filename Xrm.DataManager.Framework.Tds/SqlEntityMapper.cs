using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// Convert SQL rows to entities :
    /// - primary key is used as record id
    /// - other columns are stored in FormattedValues as invariant strings (read them with GetSqlValue)
    /// Attributes are left empty : record can be used for Update without sending SQL columns
    /// </summary>
    internal static class SqlEntityMapper
    {
        public static List<Entity> ToEntities(IDataReader reader, string entityName, string primaryKey)
        {
            var columns = new string[reader.FieldCount];
            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var primaryKeyOrdinal = -1;
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var column = reader.GetName(i);
                if (string.IsNullOrEmpty(column))
                {
                    throw new InvalidOperationException($"Column {i + 1} of SQL query result has no name : use an alias!");
                }
                if (!columnNames.Add(column))
                {
                    throw new InvalidOperationException($"Column '{column}' is returned several times by SQL query : use aliases!");
                }
                if (string.Equals(column, primaryKey, StringComparison.OrdinalIgnoreCase))
                {
                    primaryKeyOrdinal = i;
                }
                columns[i] = column;
            }

            if (primaryKeyOrdinal < 0)
            {
                throw new InvalidOperationException($"Primary key column '{primaryKey}' of table '{entityName}' is missing from SQL query result!");
            }

            var entities = new List<Entity>();
            while (reader.Read())
            {
                if (!(reader.GetValue(primaryKeyOrdinal) is Guid id))
                {
                    throw new InvalidOperationException($"Primary key column '{primaryKey}' must contain a unique identifier (row {entities.Count + 1})!");
                }

                var entity = new Entity(entityName, id);
                for (var i = 0; i < columns.Length; i++)
                {
                    if (i == primaryKeyOrdinal || reader.IsDBNull(i))
                    {
                        continue;
                    }
                    entity.FormattedValues[columns[i]] = ToInvariantString(reader.GetValue(i));
                }
                entities.Add(entity);
            }
            return entities;
        }

        /// <summary>
        /// Culture independent format, parsed by GetSqlValue
        /// </summary>
        internal static string ToInvariantString(object value)
        {
            switch (value)
            {
                case string text:
                    return text;
                case DateTime date:
                    // TDS endpoint returns UTC dates
                    return DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
                case DateTimeOffset dateOffset:
                    return dateOffset.ToString("o", CultureInfo.InvariantCulture);
                case double number:
                    return number.ToString("R", CultureInfo.InvariantCulture);
                case float number:
                    return number.ToString("R", CultureInfo.InvariantCulture);
                case bool boolean:
                    return boolean ? "true" : "false";
                case byte[] bytes:
                    return Convert.ToBase64String(bytes);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }
    }
}
