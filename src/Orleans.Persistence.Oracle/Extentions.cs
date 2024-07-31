using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Oracle.ManagedDataAccess.Client;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Principal;
namespace Orleans.Persistence.Oracle
{
    public static class Extentions
    {
        public static string GetKey(this Type type)
        {
            PropertyInfo[] properties = type.GetProperties();

            foreach (PropertyInfo property in properties)
            {
                bool hasKeyAttribute = property.GetCustomAttributes(typeof(KeyAttribute), false).Any();

                if (hasKeyAttribute)
                {
                    return property.Name;
                }
            }
            return string.Empty;
        }
        private static string GetTableName(this Type type)
        {
            var descriptionAttr = type.GetCustomAttribute<DescriptionAttribute>();
            return descriptionAttr != null ? descriptionAttr.Description : type.Name;
        }
        private static string GetSqlTypeFromAttribute(PropertyInfo property)
        {
            var descriptionAttribute = property.GetCustomAttributes(typeof(DescriptionAttribute), false)
                                               .FirstOrDefault() as DescriptionAttribute;
            if (descriptionAttribute != null)
            {
                return descriptionAttribute.Description;
            }

            throw new InvalidOperationException($"No data type of column {property.Name}");
        }
        private static string GetSqlTypeFromAttributeForeCreate(PropertyInfo property)
        {
            var descriptionAttribute = property.GetCustomAttributes(typeof(DescriptionAttribute), false)
                                               .FirstOrDefault() as DescriptionAttribute;
            if (descriptionAttribute != null)
            {
                bool hasKeyAttribute = property.GetCustomAttributes(typeof(KeyAttribute), false).Any();

                if (hasKeyAttribute)
                {
                    return $"{descriptionAttribute.Description} PRIMARY KEY";
                }
                return descriptionAttribute.Description;
            }
            throw new InvalidOperationException($"No data type of column {property.Name}");
        }
        public static async Task CreateTableIfNotExistsAsync(this DbContext context, Type type)
        {
            var tableName = type.GetTableName();
            var properties = type.GetProperties();
            var columns = properties.Select(p => $"{p.Name} {GetSqlTypeFromAttributeForeCreate(p)}").ToArray();
            var columnsJoined = string.Join(", ", columns);
            var sql = $"BEGIN EXECUTE IMMEDIATE 'CREATE TABLE {tableName} ({columnsJoined})'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -955 THEN RAISE; END IF; END;";
            await context.Database.ExecuteSqlRawAsync(sql);
        }
        public static async Task InsertEntityAsync<T>(this DbContext context, T entity)
        {
            var type = typeof(T);
            var tableName = type.GetTableName();
            var properties = type.GetProperties();
            var columns = string.Join(", ", properties.Select(p => p.Name));
            var values = string.Join(", ", properties.Select(p => $":{p.Name}"));
            var sql = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";

            var parameters = properties.Select(p => new OracleParameter($"{p.Name}", p.GetValue(entity) ?? DBNull.Value)).ToArray();

            await context.Database.ExecuteSqlRawAsync(sql, parameters);
        }
        public static async Task UpdateEntityAsync<T>(this DbContext context, T entity)
        {
            var type = typeof(T);
            var tableName = type.GetTableName();
            var properties = type.GetProperties();
            var keyProperty = properties.FirstOrDefault(p => p.Name.ToLower() == type.GetKey());
            if (keyProperty == null) throw new InvalidOperationException("No key column found.");

            var setClause = string.Join(", ", properties.Where(p => p != keyProperty).Select(p => $"{p.Name} = :{p.Name}"));
            var sql = $"UPDATE {tableName} SET {setClause} WHERE {keyProperty.Name} = :{keyProperty.Name}";

            var parameters = properties.Select(p => new OracleParameter($"{p.Name}", p.GetValue(entity) ?? DBNull.Value)).ToArray();

            await context.Database.ExecuteSqlRawAsync(sql, parameters);
        }
        public static async Task<T?> GetEntityByIdAsync<T>(this DbContext context, string key)
        {
            var type = typeof(T);
            var tableName = type.GetTableName();
            var keyProperty = type.GetProperties().FirstOrDefault(p => p.Name == type.GetKey());
            if (keyProperty == null) throw new InvalidOperationException("No key column found.");

            var sql = $"SELECT * FROM {tableName} WHERE {keyProperty.Name} = :{type.GetKey()}";
            var parameter = new OracleParameter(type.GetKey(), key);
            var rs = context.Database.SqlQueryRaw<T>(sql, parameter);
            if (rs != null && rs.Count() != 0)
            {
                return await rs.FirstOrDefaultAsync();
            }
            return default(T?);
        }
        public static async Task DeleteEntityAsync<T>(this DbContext context, string id)
        {
            var type = typeof(T);
            var tableName = type.GetTableName();
            var keyProperty = type.GetProperties().FirstOrDefault(p => p.Name.ToLower() == type.GetKey());
            if (keyProperty == null) throw new InvalidOperationException("No key column found.");

            var sql = $"DELETE FROM {tableName} WHERE {keyProperty.Name} = :{type.GetKey()}";
            var parameter = new OracleParameter(type.GetKey(), id);

            await context.Database.ExecuteSqlRawAsync(sql, parameter);
        }
    }
}
