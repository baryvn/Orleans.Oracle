using Microsoft.Extensions.DependencyInjection;
using Orleans.Messaging;
using Orleans.Runtime.Membership;
using Orleans.Configuration;

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
        public static ISiloBuilder UseOracleClustering(
            this ISiloBuilder builder,
            Action<OracleClusteringSiloOptions> configureOptions)
        {
            return builder.ConfigureServices(
                services =>
                {
                    if (configureOptions != null)
                    {
                        services.Configure(configureOptions);

                        OracleClusteringSiloOptions option = new OracleClusteringSiloOptions();
                        configureOptions.Invoke(option);
                    }

                    services.AddSingleton<IMembershipTable, OracleBasedMembershipTable>();
                });
        }

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
        public static ISiloBuilder UseOracleClustering(
            this ISiloBuilder builder, OracleClusteringSiloOptions oraOptions)
        {
            return builder.ConfigureServices(
                services =>
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
        public static IClientBuilder UseOracleClustering(
            this IClientBuilder builder, OracleGatewayListProviderOptions oraOptions)
        {
            return builder.ConfigureServices(
                services =>
                {
                    services.AddSingleton<IGatewayListProvider, OracleGatewayListProvider>();
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
        public static IClientBuilder UseOracleClustering(
            this IClientBuilder builder,
            Action<OracleGatewayListProviderOptions> configureOptions)
        {
            return builder.ConfigureServices(
                services =>
                {
                    if (configureOptions != null)
                    {
                        services.Configure(configureOptions);

                        OracleGatewayListProviderOptions option = new OracleGatewayListProviderOptions();
                        configureOptions.Invoke(option);
                    }
                    services.AddSingleton<IGatewayListProvider, OracleGatewayListProvider>();
                });
        }
    }
}
