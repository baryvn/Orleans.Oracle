using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Orleans.Oracle.Core;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Reflection;
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

        public static PropertyInfo GetKeyProp(this Type type)
        {
            PropertyInfo[] properties = type.GetProperties();

            foreach (PropertyInfo property in properties)
            {
                bool hasKeyAttribute = property.GetCustomAttributes(typeof(KeyAttribute), false).Any();

                if (hasKeyAttribute)
                {
                    return property;
                }
            }
            return null;
        }

        public static List<string> GetGroupKeyString(this Type type)
        {
            var list = new List<string>();
            PropertyInfo[] properties = type.GetProperties();

            foreach (PropertyInfo property in properties)
            {
                bool hasKeyAttribute = property.GetCustomAttributes(typeof(GroupKeyAttribute), false).Any();

                if (hasKeyAttribute)
                {
                    list.Add(property.Name);
                }
            }
            return list;
        }
        public static List<PropertyInfo> GetGroupKey(this Type type)
        {
            var list = new List<PropertyInfo>();
            PropertyInfo[] properties = type.GetProperties();

            foreach (PropertyInfo property in properties)
            {
                bool hasKeyAttribute = property.GetCustomAttributes(typeof(GroupKeyAttribute), false).Any();

                if (hasKeyAttribute)
                {
                    list.Add(property);
                }
            }
            return list;
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
        public static async Task CreateTableIfNotExistsAsync(this OracleDbContext context, Type type)
        {
            var tableName = type.GetTableName();
            var properties = type.GetProperties();
            var key = type.GetKeyProp();
            var keys = type.GetGroupKey();
            keys.Add(key);
            var pkeys = keys.Select(p => p.Name).ToArray();
            var columns = properties.Select(p => $"{p.Name} {GetSqlTypeFromAttribute(p)}").ToArray();
            var columnsJoined = string.Join(", ", columns);
            var sql = $"BEGIN EXECUTE IMMEDIATE 'CREATE TABLE {tableName} ({columnsJoined},CONSTRAINT {tableName}_PK PRIMARY KEY({string.Join(", ", pkeys)}) ENABLE)'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -955 THEN RAISE; END IF; END;";
            context.Database.ExecuteSqlRaw(sql);
        }
        public static async Task InsertAsync(this OracleDbContext context, IEnumerable<object> entitys, Type type)
        {
            var tableName = type.GetTableName();
            var properties = type.GetProperties();
            var columns = string.Join(", ", properties.Select(p => p.Name));
            var values = string.Join(", ", properties.Select(p => $":{p.Name}"));
            var insertsql = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";
            foreach (var entity in entitys)
            {
                var insertParas = properties.Select(p => new OracleParameter($"{p.Name}", p.GetValue(entity) ?? DBNull.Value)).ToArray();
                await context.Database.ExecuteSqlRawAsync(insertsql, insertParas);
            }
        }
        public static async Task UpdateAsync(this OracleDbContext context, IEnumerable<object> entitys, Type type)
        {
            var tableName = type.GetTableName();
            var key = type.GetKeyProp();
            if (key == null) throw new InvalidOperationException("No key column found.");
            var properties = type.GetProperties();
            var keys = type.GetGroupKey();
            var wheres = keys.Any() ? $"{key.Name} = :{key.Name} AND {string.Join(" AND ", keys.Select(p => $"{p.Name} = :{p.Name}"))}" : $"{key.Name} = :{key.Name}";
            var updates = string.Join(", ", properties.Select(p => $"{p.Name} = :{p.Name}"));
            var insertsql = $"UPDATE {tableName} SET {updates} WHERE {wheres}";
            foreach (var entity in entitys)
            {
                var insertParas = properties.Select(p => new OracleParameter($"{p.Name}", p.GetValue(entity) ?? DBNull.Value)).ToArray();
                await context.Database.ExecuteSqlRawAsync(insertsql, insertParas);
            }
        }
        public static async Task<List<object>> GetEntityByIdAsync(this string connectionString, string key, Type type)
        {
            var tableName = type.GetTableName();
            var keyName = type.GetKey();
            var props = type.GetProperties();
            if (string.IsNullOrEmpty(keyName)) throw new InvalidOperationException("No key column found.");
            var sql = $"SELECT * FROM {tableName} WHERE {keyName} = :{keyName}";
            var parameter = new OracleParameter(keyName, key);
            var results = new List<dynamic>();
            using (var con = new OracleConnection(connectionString))
            using (var command = con.CreateCommand())
            {
                command.Connection.KeepAlive = true;
                command.CommandText = sql;
                command.Parameters.Clear();
                command.Parameters.Add(parameter);
                try
                {
                    if (command.Connection != null)
                    {
                        if (command.Connection.State != ConnectionState.Open)
                            command.Connection.Open();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var tasks = new List<Task>();
                            while (await reader.ReadAsync())
                            {
                                var model = Activator.CreateInstance(type);
                                for (var i = 0; i < reader.FieldCount; i++)
                                {
                                    var name = reader.GetName(i);
                                    var val = reader.GetValue(i);
                                    var prop = props.FirstOrDefault(p => p.Name == name);
                                    if (prop != null)
                                    {

                                        if (!DBNull.Value.Equals(val))
                                        {
                                            object convertedValue = null;
                                            var propertyType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                                            switch (Type.GetTypeCode(propertyType))
                                            {
                                                case TypeCode.Int32:
                                                    convertedValue = Convert.ToInt32(val);
                                                    break;
                                                case TypeCode.Int64:
                                                    convertedValue = Convert.ToInt64(val);
                                                    break;
                                                case TypeCode.Int16:
                                                    convertedValue = Convert.ToInt16(val);
                                                    break;
                                                case TypeCode.Byte:
                                                    convertedValue = Convert.ToByte(val);
                                                    break;
                                                case TypeCode.UInt32:
                                                    convertedValue = Convert.ToUInt32(val);
                                                    break;
                                                case TypeCode.UInt64:
                                                    convertedValue = Convert.ToUInt64(val);
                                                    break;
                                                case TypeCode.UInt16:
                                                    convertedValue = Convert.ToUInt16(val);
                                                    break;
                                                case TypeCode.Single:
                                                    convertedValue = Convert.ToSingle(val);
                                                    break;
                                                case TypeCode.Double:
                                                    convertedValue = Convert.ToDouble(val);
                                                    break;
                                                case TypeCode.Decimal:
                                                    convertedValue = Convert.ToDecimal(val);
                                                    break;
                                                case TypeCode.Boolean:
                                                    convertedValue = Convert.ToBoolean(val);
                                                    break;
                                                case TypeCode.Char:
                                                    convertedValue = Convert.ToChar(val);
                                                    break;
                                                case TypeCode.String:
                                                    convertedValue = Convert.ToString(val);
                                                    break;
                                                case TypeCode.DateTime:
                                                    convertedValue = Convert.ToDateTime(val);
                                                    break;
                                                default:
                                                    if (prop.PropertyType == typeof(Guid))
                                                    {
                                                        convertedValue = Guid.Parse(val.ToString());
                                                    }
                                                    else if (prop.PropertyType == typeof(TimeSpan))
                                                    {
                                                        convertedValue = TimeSpan.Parse(val.ToString());
                                                    }
                                                    else
                                                    {
                                                        convertedValue = val;
                                                    }
                                                    break;
                                            }

                                            prop.SetValue(model, convertedValue);
                                        }
                                    }
                                }
                                results.Add(model);
                            }
                        }
                    }
                }
                finally
                {
                    if (command.Connection != null) command.Connection.Close();
                }

                return results;
            }
        }
        public static async Task DeleteAsync(this OracleDbContext context, IEnumerable<object> entitys, Type type)
        {
            var tableName = type.GetTableName();
            var keys = type.GetGroupKey();
            var key = type.GetKeyProp();
            var wheres = keys.Any() ? $"{key.Name} = :{key.Name} AND {string.Join(" AND ", keys.Select(p => $"{p.Name} = :{p.Name}"))}" : $"{key.Name} = :{key.Name}";
            var sql = $"DELETE FROM {tableName} WHERE {wheres}";
            foreach (var entity in entitys)
            {
                var paras = new List<OracleParameter> { new OracleParameter(key.Name, key.GetValue(entity) ?? DBNull.Value) };
                paras.AddRange(keys.Select(p => new OracleParameter($"{p.Name}", p.GetValue(entity) ?? DBNull.Value)).ToList());
                await context.Database.ExecuteSqlRawAsync(sql, paras);
            }
        }
        public static async Task DeleteEntityAsync(this OracleDbContext context, Guid id, Type type)
        {
            var tableName = type.GetTableName();
            var keyName = type.GetKey();
            if (string.IsNullOrEmpty(keyName)) throw new InvalidOperationException("No key column found.");
            var sql = $"DELETE FROM {tableName} WHERE {keyName} = :{keyName}";
            var parameter = new OracleParameter(keyName, id);
            await context.Database.ExecuteSqlRawAsync(sql, parameter);
        }
        public static bool CompareObjectKeys(Type type, object obj1, object obj2)
        {
            if (obj1 == null || obj2 == null)
                return false;
            var keys = type.GetGroupKey();
            var key = type.GetKeyProp();
            object valuekey1 = key.GetValue(obj1);
            object valuekey2 = key.GetValue(obj2);
            if (!Equals(valuekey1, valuekey2))
                return false;
            foreach (PropertyInfo property in keys)
            {
                object value1 = property.GetValue(obj1);
                object value2 = property.GetValue(obj2);

                if (!Equals(value1, value2))
                    return false;
            }

            return true;
        }
        public static bool CompareObjects(Type type, object obj1, object obj2)
        {
            if (obj1 == null || obj2 == null)
                return false;
            PropertyInfo[] properties = type.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                object value1 = property.GetValue(obj1);
                object value2 = property.GetValue(obj2);
                if (!Equals(value1, value2))
                    return false;
            }
            return true;
        }

    }
}
