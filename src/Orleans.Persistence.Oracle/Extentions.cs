using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Dynamic;
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
        public static async Task CreateTableIfNotExistsAsync(this DbContext context, Type type)
        {
            var tableName = type.GetTableName();
            var properties = type.GetProperties();
            var columns = properties.Select(p => $"{p.Name} {GetSqlTypeFromAttribute(p)}").ToArray();
            var columnsJoined = string.Join(", ", columns);
            var sql = $"BEGIN EXECUTE IMMEDIATE 'CREATE TABLE {tableName} ({columnsJoined})'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -955 THEN RAISE; END IF; END;";
            await context.Database.ExecuteSqlRawAsync(sql);
        }
        public static async Task InsertOrUpdateAsync(this DbContext context, string id, IEnumerable<object> entitys, Type type)
        {
            var tableName = type.GetTableName();
            var properties = type.GetProperties();
            var columns = string.Join(", ", properties.Select(p => p.Name));
            var values = string.Join(", ", properties.Select(p => $":{p.Name}"));
            var key = type.GetKey();
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("No key column found.");
            var clearSql = $"DELETE FROM {tableName} WHERE {key} = :{key}";
            var insertsql = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";
            using (var trans = context.Database.BeginTransaction())
            {
                try
                {
                    var clearParas = new OracleParameter(type.GetKey(), id);
                    await context.Database.ExecuteSqlRawAsync(clearSql, clearParas);
                    foreach (var entity in entitys)
                    {
                        var insertParas = properties.Select(p => new OracleParameter($"{p.Name}", p.GetValue(entity) ?? DBNull.Value)).ToArray();
                        await context.Database.ExecuteSqlRawAsync(insertsql, insertParas);
                    }
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    throw ex;
                }
            }
        }
        public static async Task<List<dynamic>> GetEntityByIdAsync(this DbContext context, string key, Type type)
        {
            var tableName = type.GetTableName();
            var keyProperty = type.GetProperties().FirstOrDefault(p => p.Name == type.GetKey());
            if (keyProperty == null) throw new InvalidOperationException("No key column found.");

            var sql = $"SELECT * FROM {tableName} WHERE {keyProperty.Name} = :{keyProperty.Name}";
            var parameter = new OracleParameter(type.GetKey(), key);

            var results = new List<dynamic>();
            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.Clear();
                command.Parameters.Add(parameter);
                if (command.Connection != null)
                {
                    if (command.Connection.State != ConnectionState.Open)
                        command.Connection.Open();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var expando = new ExpandoObject() as IDictionary<string, object>;
                            for (var i = 0; i < reader.FieldCount; i++)
                            {
                                expando[reader.GetName(i)] = reader.GetValue(i);
                            }
                            results.Add(expando);
                        }
                    }
                    await command.Connection.CloseAsync();
                    return results;
                }
                throw new Exception("ERROR: DbConnection Is null");
            }
        }
        public static async Task DeleteEntityAsync(this DbContext context, string id, Type type)
        {
            var tableName = type.GetTableName();
            var keyProperty = type.GetProperties().FirstOrDefault(p => p.Name.ToLower() == type.GetKey());
            if (keyProperty == null) throw new InvalidOperationException("No key column found.");

            var sql = $"DELETE FROM {tableName} WHERE {keyProperty.Name} = :{type.GetKey()}";
            var parameter = new OracleParameter(type.GetKey(), id);

            await context.Database.ExecuteSqlRawAsync(sql, parameter);
        }
        public static dynamic ConvertToDynamic(this object? obj)
        {
            if (obj != null)
            {
                IDictionary<string, object> expando = new ExpandoObject();
                foreach (var property in obj.GetType().GetProperties())
                {
                    expando.Add(property.Name, property.GetValue(obj));
                }
                return expando as dynamic;
            }
            throw new InvalidOperationException("Object can't null!");
        }
        public static object? ConvertToTestModel(dynamic dynamicObj, Type type)
        {
            // Create an instance of the specified type
            var model = Activator.CreateInstance(type);
            if (model != null)
            {
                // Get the properties of the model
                foreach (var property in model.GetType().GetProperties())
                {
                    // Check if the dynamic object contains the property name
                    if (((IDictionary<string, object>)dynamicObj).ContainsKey(property.Name))
                    {
                        // Get the value from the dynamic object
                        var value = ((IDictionary<string, object>)dynamicObj)[property.Name];

                        // Convert the value to the type of the property
                        var targetType = property.PropertyType;
                        object convertedValue = Convert.ChangeType(value, targetType);

                        // Set the value of the property
                        property.SetValue(model, convertedValue);
                    }
                    else
                    {
                        // Handle missing property (optional)
                        Console.WriteLine($"Property {property.Name} not found in dynamic object.");
                    }
                }
            }

            return model;
        }
    }
}
