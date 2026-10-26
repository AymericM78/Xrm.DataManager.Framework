using Microsoft.Xrm.Sdk;
using System;
using System.Globalization;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// Read columns of records retrieved with SQL (stored in FormattedValues)
    /// </summary>
    public static class SqlEntityExtensions
    {
        /// <summary>
        /// Get SQL column value
        /// </summary>
        /// <typeparam name="T">string, Guid, int, decimal, bool, DateTime (UTC), enum... nullable types are supported</typeparam>
        /// <param name="record"></param>
        /// <param name="column">Column name as returned by SQL query (lookup : 'parentcustomerid' / 'parentcustomeridname', choice : 'statecode' / 'statecodename')</param>
        /// <returns>Default value if column is null or not returned</returns>
        public static T GetSqlValue<T>(this Entity record, string column)
        {
            if (!record.FormattedValues.TryGetValue(column, out var value) || value == null)
            {
                return default(T);
            }

            var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            object result;
            if (type == typeof(string))
            {
                result = value;
            }
            else if (type == typeof(Guid))
            {
                result = Guid.Parse(value);
            }
            else if (type == typeof(DateTime))
            {
                result = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            else if (type == typeof(DateTimeOffset))
            {
                result = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            else if (type == typeof(byte[]))
            {
                result = Convert.FromBase64String(value);
            }
            else if (type.IsEnum)
            {
                result = Enum.ToObject(type, long.Parse(value, CultureInfo.InvariantCulture));
            }
            else
            {
                result = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            }
            return (T)result;
        }

        /// <summary>
        /// Get lookup column as EntityReference (name is read from '{column}name' column if returned)
        /// </summary>
        /// <param name="record"></param>
        /// <param name="column">Lookup column (ex : parentcustomerid)</param>
        /// <param name="targetEntityName">Referenced table (ex : account)</param>
        /// <returns>Null if lookup is empty</returns>
        public static EntityReference GetSqlEntityReference(this Entity record, string column, string targetEntityName)
        {
            var id = record.GetSqlValue<Guid?>(column);
            if (!id.HasValue)
            {
                return null;
            }
            return new EntityReference(targetEntityName, id.Value)
            {
                Name = record.GetSqlValue<string>($"{column}name")
            };
        }
    }
}
