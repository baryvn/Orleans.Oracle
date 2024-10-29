using Microsoft.Extensions.DependencyInjection;
using Orleans.Messaging;
using Orleans.Runtime.Membership;

namespace Orleans.Hosting
{

    public static class OracleHostingExtensions
    {
        /// <summary>
        /// Configures the silo to use Rqlite for cluster membership.
        /// </summary>
        /// <param name="builder">
        /// The builder.
        /// </param>
        /// <param name="configureOptions">
        /// The configuration delegate.
        /// </param>
        /// <returns>
        /// The provided <see cref="ISiloBuilder"/>.
        /// </returns>
        public static ISiloBuilder UseOracleClustering(this ISiloBuilder builder)
        {
            return builder.ConfigureServices(services =>
            {
                services.AddSingleton<IMembershipTable, OracleBasedMembershipTable>();
            });
        }



        /// <summary>
        /// Configure the client to use Rqlite for clustering.
        /// </summary>
        /// <param name="builder">
        /// The builder.
        /// </param>
        /// <param name="configureOptions">
        /// The configuration delegate.
        /// </param>
        /// <returns>
        /// The provided <see cref="IClientBuilder"/>.
        /// </returns>
        public static IClientBuilder UseOracleClustering(this IClientBuilder builder)
        {
            return builder.ConfigureServices(services =>
            {
                services.AddSingleton<IGatewayListProvider, OracleGatewayListProvider>();
            });
        }
    }
}
