using Orleans.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Clustering.Oracle;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Orleans.Runtime.Membership
{
    public class OracleGatewayListProvider : IGatewayListProvider
    {
        private readonly ILogger logger;
        private readonly string ClusterId;
        private readonly TimeSpan _maxStaleness;
        private readonly IServiceProvider _provider;
        private readonly OracleGatewayListProviderOptions _options;
        public bool IsInitialized { get; private set; }

        public OracleGatewayListProvider(
            ILogger<OracleGatewayListProvider> logger,
            IOptions<GatewayOptions> gatewayOptions,
            IServiceProvider provider,
            IOptions<OracleGatewayListProviderOptions> gatewayListProviderOptionss,
            IOptions<ClusterOptions> clusterOptions)
        {
            this.logger = logger;
            ClusterId = clusterOptions.Value.ClusterId;
            _provider = provider;
            _maxStaleness = gatewayOptions.Value.GatewayListRefreshPeriod;
            _options = gatewayListProviderOptionss.Value;
        }

        /// <summary>
        /// Initializes the Rqlite based gateway provider
        /// </summary>
        public Task InitializeGatewayListProvider() => Task.CompletedTask;

        /// <summary>
        /// Returns the list of gateways (silos) that can be used by a client to connect to Orleans cluster.
        /// The Uri is in the form of: "gwy.tcp://IP:port/Generation". See Utils.ToGatewayUri and Utils.ToSiloAddress for more details about Uri format.
        /// </summary>
        public async Task<IList<Uri>> GetGateways()
        {



            IList<Uri> dataRs = new List<Uri>();
            var optionsBuilder = new DbContextOptionsBuilder<ClustringContext>();
            optionsBuilder.UseOracle(_options.ConnectionString);
            using(var _context = new ClustringContext(optionsBuilder.Options))
            {
                if (!IsInitialized)
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
                    IsInitialized = true;
                }


                var query = $"SELECT * FROM {ClusterId}_Members";
                var result = await _context.Database.SqlQueryRaw<MemberModel>(query).ToListAsync();
                if (result.Any())
                {
                    foreach (var item in result)
                    {
                        var data = JsonSerializer.Deserialize<MembershipEntry>(item.Data);
                        if (data != null && data.Status == SiloStatus.Active && data.ProxyPort > 0)
                        {
                            data.SiloAddress.Endpoint.Port = data.ProxyPort;
                            dataRs.Add(data.SiloAddress.ToGatewayUri());
                        }
                    }
                }
            }
            return dataRs;
        }

        /// <summary>
        /// Specifies how often this IGatewayListProvider is refreshed, to have a bound on max staleness of its returned information.
        /// </summary>
        public TimeSpan MaxStaleness => _maxStaleness;

        /// <summary>
        /// Specifies whether this IGatewayListProvider ever refreshes its returned information, or always returns the same gw list.
        /// (currently only the static config based StaticGatewayListProvider is not updatable. All others are.)
        /// </summary>
        public bool IsUpdatable => true;
    }
}
//tôi muốn tạo một lớp triển khai interface trên thì cần luu ý điều gì