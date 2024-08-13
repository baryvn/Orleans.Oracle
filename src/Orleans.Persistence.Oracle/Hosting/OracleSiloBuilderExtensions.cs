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
    public static ISiloBuilder AddOracleGrainStorageAsDefault<T>(this ISiloBuilder builder, Action<OracleGrainStorageOptions> options) where T : DbContext
    {
        return builder.AddOracleGrainStorage<T>(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME, options);
    }

    public static ISiloBuilder AddOracleGrainStorage<T>(this ISiloBuilder builder, string providerName, Action<OracleGrainStorageOptions> options) where T : DbContext
    {
        return builder.ConfigureServices(services => services.AddOracleGrainStorage<T>(providerName, options));
    }

    public static IServiceCollection AddOracleGrainStorage<T>(this IServiceCollection services, string providerName, Action<OracleGrainStorageOptions> options) where T : DbContext
    {
        services.AddOptions<OracleGrainStorageOptions>(providerName).Configure(options);

        OracleGrainStorageOptions option = new OracleGrainStorageOptions { GrainStorageSerializer = null };
        options.Invoke(option);
        services.AddDbContextPool<T>(options => options.UseOracle(option.ConnectionString));

        services.AddTransient<IPostConfigureOptions<OracleGrainStorageOptions>, DefaultStorageProviderSerializerOptionsConfigurator<OracleGrainStorageOptions>>();

        return services.AddGrainStorage(providerName, OracleGrainStorageFactory<T>.Create);
    }
}