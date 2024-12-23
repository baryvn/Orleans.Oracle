﻿using Orleans.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Clustering.Oracle;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Oracle.Core;

namespace Orleans.Runtime.Membership
{
    public class OracleGatewayListProvider : IGatewayListProvider
    {
        private readonly ILogger logger;
        private readonly string ClusterId;
        private readonly TimeSpan _maxStaleness;
        private readonly IServiceProvider _provider;
        public bool IsInitialized { get; private set; }

        public OracleGatewayListProvider(
            ILogger<OracleGatewayListProvider> logger,
            IOptions<GatewayOptions> gatewayOptions,
            IServiceProvider provider,
            IOptions<ClusterOptions> clusterOptions)
        {
            this.logger = logger;
            ClusterId = clusterOptions.Value.ClusterId;
            _provider = provider;
            _maxStaleness = gatewayOptions.Value.GatewayListRefreshPeriod;
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
            using (var scope = _provider.CreateAsyncScope())
            using (var _context = scope.ServiceProvider.GetService<OracleDbContext>())
            {
                if (_context == null)
                {
                    throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                }
                try
                {
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
                catch (Exception ex)
                {
                    logger.LogWarning(ex, ex.Message);
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