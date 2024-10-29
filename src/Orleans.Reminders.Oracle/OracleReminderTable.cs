using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using Orleans.Configuration;
using Orleans.Oracle.Core;
using System.Collections.Immutable;
using System.Data;
using System.Text.Json;

namespace Orleans.Reminders.Oracle
{
    public class OracleReminderTable : IReminderTable
    {
        private readonly ILogger logger;
        private readonly IServiceProvider _provider;
        private readonly ClusterOptions _clusterOptions;

        public OracleReminderTable(
            ILogger<OracleReminderTable> logger,
            IOptions<ClusterOptions> clusterOptions,
            IServiceProvider provider)
        {
            this.logger = logger;
            _provider = provider;
            _clusterOptions = clusterOptions.Value;
        }
        public async Task Init()
        {
            try
            {
                using (var scope = _provider.CreateAsyncScope())
                using (var _context = scope.ServiceProvider.GetService<OracleDbContext>())
                {
                    if (_context == null) throw new Exception("DbContext is null");
                    var sql = $@"
                                    BEGIN
                                        EXECUTE IMMEDIATE 'CREATE TABLE {_clusterOptions.ServiceId}_REMINDERS (
                                            GRAIN_ID VARCHAR2(255), 
                                            REMINDER_NAME VARCHAR2(500),
                                            ETAG VARCHAR2(255),
                                            GRAIN_HASH NUMBER(10, 0),
                                            DATA CLOB
                                        )';
                                    EXCEPTION
                                        WHEN OTHERS THEN
                                            IF SQLCODE = -955 THEN
                                                -- Table already exists, ignore the error
                                                NULL;
                                            ELSE
                                                RAISE;
                                            END IF;
                                    END;
                                ";
                    await _context.Database.ExecuteSqlRawAsync(sql);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }

        public async Task<ReminderEntry> ReadRow(GrainId grainId, string reminderName)
        {
            using (var scope = _provider.CreateAsyncScope())
            using (var _context = scope.ServiceProvider.GetService<OracleDbContext>())
            {
                if (_context == null) throw new Exception("DbContext is null");
                var query = $"SELECT * FROM {_clusterOptions.ServiceId}_REMINDERS WHERE GRAIN_ID = :GRAIN_ID AND REMINDER_NAME = :REMINDER_NAME";
                var paras = new[] {
                    new OracleParameter("GRAIN_ID", grainId.ToString()),
                    new OracleParameter("REMINDER_NAME", reminderName)
                };

                var result = _context.Database.SqlQueryRaw<ReminderModel>(query, paras).FirstOrDefault();
                if (result != null)
                {
                    var entry = JsonSerializer.Deserialize<ReminderEntry>(result.DATA);
                    if (entry == null) throw new Exception("ReminderEntry is null");
                    return entry;
                }
            }
            return null;
        }

        public async Task<ReminderTableData> ReadRows(GrainId grainId)
        {
            using (var scope = _provider.CreateAsyncScope())
            using (var _context = scope.ServiceProvider.GetService<OracleDbContext>())
            {
                if (_context == null) throw new Exception("DbContext is null");
                var query = $"SELECT * FROM {_clusterOptions.ServiceId}_REMINDERS WHERE GRAIN_ID = :GRAIN_ID";
                var paras = new[] { new OracleParameter("GRAIN_ID", grainId.ToString()) };

                var results = _context.Database.SqlQueryRaw<ReminderModel>(query, paras).ToImmutableList();
                if (results != null)
                {
                    return new ReminderTableData(results.Select(p => JsonSerializer.Deserialize<ReminderEntry>(p.DATA)));
                }
            }
            return new ReminderTableData();
        }

        public async Task<ReminderTableData> ReadRows(uint begin, uint end)
        {
            using (var scope = _provider.CreateAsyncScope())
            using (var _context = scope.ServiceProvider.GetService<OracleDbContext>())
            {
                if (_context == null) throw new Exception("DbContext is null");
                var query = begin < end ?
                    $"SELECT * FROM {_clusterOptions.ServiceId}_REMINDERS WHERE GRAIN_HASH > :BEGIN_HASH AND GRAIN_HASH <= :END_HASH" :
                    $"SELECT * FROM {_clusterOptions.ServiceId}_REMINDERS WHERE GRAIN_HASH > :BEGIN_HASH OR GRAIN_HASH <= :END_HASH";
                var paras = new[] {
                    new OracleParameter("BEGIN_HASH", OracleDbType.Int32){ Value = begin },
                    new OracleParameter("END_HASH", OracleDbType.Int32){ Value = end }
                };
                var results = _context.Database.SqlQueryRaw<ReminderModel>(query, paras).ToImmutableList();
                if (results != null)
                {
                    return new ReminderTableData(results.Select(p => JsonSerializer.Deserialize<ReminderEntry>(p.DATA)));
                }
            }
            return new ReminderTableData();
        }

        public async Task<string> UpsertRow(ReminderEntry entry)
        {
            string etag = Guid.NewGuid().ToString();
            using (var scope = _provider.CreateAsyncScope())
            using (var _context = scope.ServiceProvider.GetService<OracleDbContext>())
            {
                if (_context == null) throw new Exception("DbContext is null");
                var selectQuery = $"SELECT * FROM {_clusterOptions.ServiceId}_REMINDERS WHERE GRAIN_ID = :GRAIN_ID AND REMINDER_NAME = :REMINDER_NAME";
                var paras = new[] {
                    new OracleParameter("GRAIN_ID", entry.GrainId.ToString()),
                    new OracleParameter("REMINDER_NAME", entry.ReminderName)
                };
                var existing = _context.Database.SqlQueryRaw<ReminderModel>(selectQuery, paras).ToImmutableList();
                if (!existing.Any())
                {
                    entry.ETag = etag;
                    var insertQuery = $"INSERT INTO {_clusterOptions.ServiceId}_REMINDERS (GRAIN_ID,REMINDER_NAME, ETAG,GRAIN_HASH,DATA) VALUES (:GRAIN_ID,:REMINDER_NAME, :ETAG,:GRAIN_HASH,:DATA)";
                    paras = [
                        new OracleParameter("GRAIN_ID", entry.GrainId.ToString()),
                        new OracleParameter("REMINDER_NAME", entry.ReminderName),
                        new OracleParameter("ETAG", etag),
                        new OracleParameter("GRAIN_HASH",  OracleDbType.Int32){ Value = entry.GrainId.GetUniformHashCode() },
                        new OracleParameter("DATA", JsonSerializer.Serialize(entry))
                    ];
                    await _context.Database.ExecuteSqlRawAsync(insertQuery, paras);
                }
                else
                {
                    var updateQuery = $"UPDATE {_clusterOptions.ServiceId}_REMINDERS SET DATA = :DATA, ETAG = :ETAG, GRAIN_HASH = :GRAIN_HASH WHERE GRAIN_ID = :GRAIN_ID AND REMINDER_NAME = :REMINDER_NAME";
                    paras = [
                        new OracleParameter("GRAIN_ID", entry.GrainId.ToString()),
                        new OracleParameter("REMINDER_NAME", entry.ReminderName),
                        new OracleParameter("ETAG", entry.ETag),
                        new OracleParameter("GRAIN_HASH", OracleDbType.Int32){ Value = entry.GrainId.GetUniformHashCode() },
                        new OracleParameter("DATA", JsonSerializer.Serialize(entry))
                    ];
                    await _context.Database.ExecuteSqlRawAsync(updateQuery, paras);
                }
            }
            return etag;
        }

        public async Task<bool> RemoveRow(GrainId grainId, string reminderName, string eTag)
        {
            using (var scope = _provider.CreateAsyncScope())
            using (var _context = scope.ServiceProvider.GetService<OracleDbContext>())
            {
                if (_context == null) throw new Exception("DbContext is null");
                var query = $"DELETE {_clusterOptions.ServiceId}_REMINDERS WHERE GRAIN_ID = :GRAIN_ID AND REMINDER_NAME = :REMINDER_NAME AND ETAG = :ETAG";
                var paras = new[] {
                    new OracleParameter("GRAIN_ID", grainId.ToString()),
                    new OracleParameter("REMINDER_NAME", reminderName),
                    new OracleParameter("ETAG", eTag)
                };

                var result = await _context.Database.ExecuteSqlRawAsync(query, paras);
                return result > 0;
            }
        }

        public async Task TestOnlyClearTable()
        {
            using (var scope = _provider.CreateAsyncScope())
            using (var _context = scope.ServiceProvider.GetService<OracleDbContext>())
            {
                if (_context == null) throw new Exception("DbContext is null");
                var memberQuery = $"DELETE {_clusterOptions.ServiceId}_REMINDERS";
                var memberResult = await _context.Database.ExecuteSqlRawAsync(memberQuery);
            }
        }
    }
}
