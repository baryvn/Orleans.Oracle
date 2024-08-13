using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Hosting;
using Orleans.Persistence.Oracle.Providers;
using Orleans.Persistence.Oracle.Storage;
using Orleans.Providers;
using Orleans.Runtime.Hosting;
using Orleans.Storage;

namespace Orleans.Persistence.Oracle.Hosting;

public static class OracleSiloBuilderExtensions
{
    public static ISiloBuilder AddOracleGrainStorageAsDefault(this ISiloBuilder builder, Action<OracleGrainStorageOptions> options) 
    {
        return builder.AddOracleGrainStorage(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME, options);
    }

    public static ISiloBuilder AddOracleGrainStorage(this ISiloBuilder builder, string providerName, Action<OracleGrainStorageOptions> options) 
    {
        return builder.ConfigureServices(services => services.AddOracleGrainStorage(providerName, options));
    }

    public static IServiceCollection AddOracleGrainStorage(this IServiceCollection services, string providerName, Action<OracleGrainStorageOptions> options)
    {
        services.AddOptions<OracleGrainStorageOptions>(providerName).Configure(options);

        OracleGrainStorageOptions option = new OracleGrainStorageOptions { GrainStorageSerializer = null };
        options.Invoke(option);

        services.AddTransient<IPostConfigureOptions<OracleGrainStorageOptions>, DefaultStorageProviderSerializerOptionsConfigurator<OracleGrainStorageOptions>>();

        return services.AddGrainStorage(providerName, OracleGrainStorageFactory.Create);
    }
}