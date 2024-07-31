
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Clustering.Oracle;
using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Orleans.Runtime.Membership
{

    public class OracleBasedMembershipTable : IMembershipTable
    {
        private readonly ILogger logger;
        private readonly IServiceProvider _provider;

        private readonly string ClusterId;
        private static readonly TableVersion DefaultTableVersion = new TableVersion(0, "0");

        public bool IsInitialized { get; private set; }
        public OracleBasedMembershipTable(
            ILogger<OracleBasedMembershipTable> logger,
            IServiceProvider provider,
            IOptions<ClusterOptions> clusterOptions)
        {
            this.logger = logger;
            ClusterId = clusterOptions.Value.ClusterId;
            _provider = provider;
        }
        /// <summary>
        /// Initialize Membership Table
        /// </summary>
        /// <param name="tryInitTableVersion"></param>
        /// <returns></returns>
        public async Task InitializeMembershipTable(bool tryInitTableVersion)
        {
            if (tryInitTableVersion)
            {
                try
                {
                    using (var scope = _provider.CreateAsyncScope())
                    {
                        var _context = scope.ServiceProvider.GetService<OraDbContext>();
                        if (_context != null)
                        {

                            // Thực hiện các lệnh SQL để tạo bảng
                            await _context.Database.ExecuteSqlRawAsync($@"
                                                                BEGIN
                                                                    EXECUTE IMMEDIATE 'CREATE TABLE {ClusterId}_Members (
                                                                        SiloAddress VARCHAR2(255) PRIMARY KEY, 
                                                                        Data CLOB,
                                                                        IAmAliveTime TIMESTAMP,
                                                                        Status INTEGER
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
                                                            ");
                            await _context.Database.ExecuteSqlRawAsync($@"
                                                                BEGIN
                                                                    EXECUTE IMMEDIATE 'CREATE TABLE TableVersion (
                                                                        ClusterId VARCHAR2(255) PRIMARY KEY, 
                                                                        Data CLOB
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
                                                            ");

                            // Kiểm tra sự tồn tại của dữ liệu trong TableVersion
                            var sqlQuery = $@"
                                        SELECT COUNT(*) as COUNT
                                        FROM TableVersion 
                                        WHERE ClusterId = :ClusterId
                                    ";

                            var count = await _context.Database.SqlQueryRaw<CountModel>(sqlQuery,
                                new OracleParameter("ClusterId", this.ClusterId)
                            ).FirstOrDefaultAsync();

                            if (count != null && count.COUNT == 0)
                            {
                                // Chèn dữ liệu vào TableVersion
                                var insertCommand = $@"
                                                INSERT INTO TableVersion (ClusterId, Data) 
                                                VALUES (:ClusterId, :Data)
                                            ";

                                await _context.Database.ExecuteSqlRawAsync(insertCommand,
                                    new OracleParameter("ClusterId", this.ClusterId),
                                    new OracleParameter("Data", JsonSerializer.Serialize(DefaultTableVersion))
                                );
                            }

                        }
                    }
                    IsInitialized = true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, ex.Message);
                }
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Atomically reads the Membership Table information about a given silo.
        /// The returned MembershipTableData includes one MembershipEntry entry for a given silo and the 
        /// TableVersion for this table. The MembershipEntry and the TableVersion have to be read atomically.
        /// </summary>
        /// <param name="siloAddress">The address of the silo whose membership information needs to be read.</param>
        /// <returns>The membership information for a given silo: MembershipTableData consisting one MembershipEntry entry and
        /// TableVersion, read atomically.</returns>
        public async Task<MembershipTableData> ReadRow(SiloAddress siloAddress)
        {
            try
            {
                TableVersion tableVersion = DefaultTableVersion;
                using (var scope = _provider.CreateAsyncScope())
                {
                    var _context = scope.ServiceProvider.GetService<OraDbContext>();
                    if (_context != null)
                    {
                        // Truy vấn dữ liệu TableVersion từ Oracle
                        var tableVersionQuery = "SELECT * FROM TableVersion WHERE ClusterId = :ClusterId";
                        var tableVersionParams = new[] { new OracleParameter("ClusterId", ClusterId) };

                        var tableVersionResults = await _context.Database
                            .SqlQueryRaw<TableVersionModel>(tableVersionQuery, tableVersionParams)
                            .ToListAsync();

                        if (tableVersionResults.Any())
                        {
                            var state = tableVersionResults.Single();
                            var table = JsonSerializer.Deserialize<TableVersionData>(state.Data);
                            if (table != null)
                            {
                                tableVersion = new TableVersion(table.Version, table.VersionEtag);
                            }
                        }

                        // Truy vấn dữ liệu MemberModel từ Oracle
                        var memberQuery = $"SELECT * FROM {ClusterId}_Members WHERE SiloAddress = :SiloAddress";
                        var memberParams = new[] { new OracleParameter("SiloAddress", siloAddress.ToString()) };

                        var memberResults = await _context.Database
                            .SqlQueryRaw<MemberModel>(memberQuery, memberParams)
                            .ToListAsync();

                        var member = memberResults
                            .Select(p => Tuple.Create(JsonSerializer.Deserialize<MembershipEntry>(p.Data), tableVersion.VersionEtag))
                            .FirstOrDefault();

                        if (member != null)
                        {
                            return new MembershipTableData(member, tableVersion);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }

            return new MembershipTableData(DefaultTableVersion);
        }
        /// <summary>
        /// Atomically reads the full content of the Membership Table.
        /// The returned MembershipTableData includes all MembershipEntry entry for all silos in the table and the 
        /// TableVersion for this table. The MembershipEntries and the TableVersion have to be read atomically.
        /// </summary>
        /// <returns>The membership information for a given table: MembershipTableData consisting multiple MembershipEntry entries and
        /// TableVersion, all read atomically.</returns>
        public async Task<MembershipTableData> ReadAll()
        {
            try
            {
                TableVersion tableVersion = DefaultTableVersion;
                using (var scope = _provider.CreateAsyncScope())
                {
                    var _context = scope.ServiceProvider.GetService<OraDbContext>();
                    if (_context != null)
                    {
                        // Query for TableVersion
                        var tableVersionQuery = "SELECT * FROM TableVersion WHERE ClusterId = :ClusterId";
                        var tableVersionResult = await _context.Database
                            .SqlQueryRaw<TableVersionModel>(tableVersionQuery, new OracleParameter("ClusterId", this.ClusterId))
                            .ToListAsync();

                        if (tableVersionResult.Any())
                        {
                            var state = tableVersionResult.Single();
                            var table = JsonSerializer.Deserialize<TableVersionData>(state.Data);
                            if (table != null)
                            {
                                tableVersion = new TableVersion(table.Version, table.VersionEtag);
                            }
                        }

                        // Query for Members
                        var membersQuery = $"SELECT * FROM {this.ClusterId}_Members";
                        var membersResult = await _context.Database
                            .SqlQueryRaw<MemberModel>(membersQuery)
                            .ToListAsync();

                        var members = membersResult
                            .Select(p => Tuple.Create(JsonSerializer.Deserialize<MembershipEntry>(p.Data), tableVersion.VersionEtag))
                            .ToList();

                        return new MembershipTableData(members, tableVersion);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }

            return new MembershipTableData(DefaultTableVersion);
        }

        /// <summary>
        /// Atomically tries to insert (add) a new MembershipEntry for one silo and also update the TableVersion.
        /// If operation succeeds, the following changes would be made to the table:
        /// 1) New MembershipEntry will be added to the table.
        /// 2) The newly added MembershipEntry will also be added with the new unique automatically generated eTag.
        /// 3) TableVersion.Version in the table will be updated to the new TableVersion.Version.
        /// 4) TableVersion etag in the table will be updated to the new unique automatically generated eTag.
        /// All those changes to the table, insert of a new row and update of the table version and the associated etags, should happen atomically, or fail atomically with no side effects.
        /// The operation should fail in each of the following conditions:
        /// 1) A MembershipEntry for a given silo already exist in the table
        /// 2) Update of the TableVersion failed since the given TableVersion etag (as specified by the TableVersion.VersionEtag property) did not match the TableVersion etag in the table.
        /// </summary>
        /// <param name="entry">MembershipEntry to be inserted.</param>
        /// <param name="tableVersion">The new TableVersion for this table, along with its etag.</param>
        /// <returns>True if the insert operation succeeded and false otherwise.</returns>
        public async Task<bool> InsertRow(MembershipEntry entry, TableVersion tableVersion)
        {
            return await InsertOrUpdateMember(entry, tableVersion, true);
        }

        /// <summary>
        /// Atomically tries to update the MembershipEntry for one silo and also update the TableVersion.
        /// If operation succeeds, the following changes would be made to the table:
        /// 1) The MembershipEntry for this silo will be updated to the new MembershipEntry (the old entry will be fully substituted by the new entry) 
        /// 2) The eTag for the updated MembershipEntry will also be eTag with the new unique automatically generated eTag.
        /// 3) TableVersion.Version in the table will be updated to the new TableVersion.Version.
        /// 4) TableVersion etag in the table will be updated to the new unique automatically generated eTag.
        /// All those changes to the table, update of a new row and update of the table version and the associated etags, should happen atomically, or fail atomically with no side effects.
        /// The operation should fail in each of the following conditions:
        /// 1) A MembershipEntry for a given silo does not exist in the table
        /// 2) A MembershipEntry for a given silo exist in the table but its etag in the table does not match the provided etag.
        /// 3) Update of the TableVersion failed since the given TableVersion etag (as specified by the TableVersion.VersionEtag property) did not match the TableVersion etag in the table.
        /// </summary>
        /// <param name="entry">MembershipEntry to be updated.</param>
        /// <param name="etag">The etag  for the given MembershipEntry.</param>
        /// <param name="tableVersion">The new TableVersion for this table, along with its etag.</param>
        /// <returns>True if the update operation succeeded and false otherwise.</returns>
        public async Task<bool> UpdateRow(MembershipEntry entry, string etag, TableVersion tableVersion)
        {
            return await InsertOrUpdateMember(entry, tableVersion, true);
        }

        private async Task<bool> InsertOrUpdateMember(MembershipEntry entry, TableVersion tableVersion, bool updateTableVersion)
        {
            try
            {
                using (var scope = _provider.CreateAsyncScope())
                {
                    var _context = scope.ServiceProvider.GetService<OraDbContext>();
                    if (_context != null)
                    {
                        if (updateTableVersion)
                        {
                            if (tableVersion.Version == 0 && "0".Equals(tableVersion.VersionEtag, StringComparison.Ordinal))
                            {
                                var selectQuery = $"SELECT * FROM TableVersion WHERE ClusterId = :ClusterId";
                                var existingTableVersion = await _context.Database.SqlQueryRaw<TableVersionModel>(selectQuery, new OracleParameter("ClusterId", ClusterId)).ToListAsync();
                                if (!existingTableVersion.Any())
                                {
                                    var insertQuery = "INSERT INTO TableVersion (ClusterId, Data) VALUES (:ClusterId, :Data)";
                                    await _context.Database.ExecuteSqlRawAsync(insertQuery,
                                        new OracleParameter("ClusterId", ClusterId),
                                        new OracleParameter("Data", JsonSerializer.Serialize(DefaultTableVersion)));
                                }
                            }
                            else
                            {
                                var updateQuery = "UPDATE TableVersion SET Data = :Data WHERE ClusterId = :ClusterId";
                                await _context.Database.ExecuteSqlRawAsync(updateQuery,
                                     new OracleParameter("Data", JsonSerializer.Serialize(tableVersion)),
                                      new OracleParameter("ClusterId", ClusterId));
                            }
                        }

                        var memberQuery = $"SELECT * FROM {ClusterId}_Members WHERE SiloAddress = :SiloAddress";
                        var member = await _context.Database.SqlQueryRaw<MemberModel>(memberQuery, new OracleParameter("SiloAddress", entry.SiloAddress.ToString())).ToListAsync();

                        if (!member.Any())
                        {
                            var insertMemberQuery = $"INSERT INTO {ClusterId}_Members (SiloAddress, Data,IAmAliveTime,Status) VALUES (:SiloAddress, :Data, :IAmAliveTime, :Status)";
                            await _context.Database.ExecuteSqlRawAsync(insertMemberQuery,
                                new OracleParameter("SiloAddress", entry.SiloAddress.ToString()),
                                new OracleParameter("IAmAliveTime", entry.IAmAliveTime),
                                new OracleParameter("Status", (int)entry.Status),
                                new OracleParameter("Data", JsonSerializer.Serialize(entry)));
                        }
                        else
                        {
                            var updateMemberQuery = $"UPDATE {ClusterId}_Members SET Data = :Data, IAmAliveTime = :IAmAliveTime,Status = :Status WHERE SiloAddress = :SiloAddress";
                            await _context.Database.ExecuteSqlRawAsync(updateMemberQuery,
                                new OracleParameter("SiloAddress", entry.SiloAddress.ToString()),
                                new OracleParameter("IAmAliveTime", entry.IAmAliveTime),
                                new OracleParameter("Status", (int)entry.Status),
                                new OracleParameter("Data", JsonSerializer.Serialize(entry)));
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, ex, ex.Message);
            }
            return false;
        }
        /// <summary>
        /// Updates the IAmAlive part (column) of the MembershipEntry for this silo.
        /// This operation should only update the IAmAlive column and not change other columns.
        /// This operation is a "dirty write" or "in place update" and is performed without etag validation. 
        /// With regards to eTags update:
        /// This operation may automatically update the eTag associated with the given silo row, but it does not have to. It can also leave the etag not changed ("dirty write").
        /// With regards to TableVersion:
        /// this operation should not change the TableVersion of the table. It should leave it untouched.
        /// There is no scenario where this operation could fail due to table semantical reasons. It can only fail due to network problems or table unavailability.
        /// </summary>
        /// <param name="entry">The target MembershipEntry tp update</param>
        /// <returns>Task representing the successful execution of this operation. </returns>
        public async Task UpdateIAmAlive(MembershipEntry entry)
        {
            try
            {
                TableVersion tableVersion = DefaultTableVersion;
                using (var scope = _provider.CreateAsyncScope())
                {
                    var _context = scope.ServiceProvider.GetService<OraDbContext>();
                    if (_context != null)
                    {
                        // Lấy thông tin phiên bản bảng từ Oracle
                        var selectTableVersionQuery = "SELECT * FROM TableVersion WHERE ClusterId = :ClusterId";
                        var existingTableVersion = await _context.Database.SqlQueryRaw<TableVersionModel>(selectTableVersionQuery, new OracleParameter("ClusterId", ClusterId)).ToListAsync();

                        if (existingTableVersion.Any())
                        {
                            var state = existingTableVersion.Single();
                            var table = JsonSerializer.Deserialize<TableVersionData>(state.Data);
                            if (table != null)
                            {
                                tableVersion = new TableVersion(table.Version, table.VersionEtag).Next();
                            }
                        }

                        // Lấy thông tin thành viên từ Oracle
                        var selectMemberQuery = $"SELECT * FROM {ClusterId}_Members WHERE SiloAddress = :SiloAddress";
                        var result = await _context.Database.SqlQueryRaw<MemberModel>(selectMemberQuery, new OracleParameter("SiloAddress", entry.SiloAddress.ToString())).ToListAsync();
                        var member = result.Select(p => JsonSerializer.Deserialize<MembershipEntry>(p.Data)).FirstOrDefault();

                        if (member != null)
                        {
                            member.IAmAliveTime = entry.IAmAliveTime;
                            await InsertOrUpdateMember(member, tableVersion, updateTableVersion: false);
                        }
                        else
                        {
                            throw new Exception("Member not found!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
        /// <summary>
        /// Deletes all table entries of the given clusterId
        /// </summary>

        public async Task DeleteMembershipTableEntries(string clusterId)
        {
            try
            {
                using (var scope = _provider.CreateAsyncScope())
                {
                    var _context = scope.ServiceProvider.GetService<OraDbContext>();
                    if (_context != null)
                    {
                        var deleteQuery = $"DELETE FROM {clusterId}_Members";
                        await _context.Database.ExecuteSqlRawAsync(deleteQuery);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
        public async Task CleanupDefunctSiloEntries(DateTimeOffset beforeDate)
        {
            try
            {
                using (var scope = _provider.CreateAsyncScope())
                {
                    var _context = scope.ServiceProvider.GetService<OraDbContext>();
                    if (_context != null)
                    {
                        var cleanupQuery = $@"  DELETE FROM {ClusterId}_Members 
                                        WHERE  Status = :Status AND IAmAliveTime < :BeforeDate";

                        await _context.Database.ExecuteSqlRawAsync(cleanupQuery,
                            new OracleParameter("Status", (int)SiloStatus.Dead),
                            new OracleParameter("BeforeDate", beforeDate));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
    }


}
