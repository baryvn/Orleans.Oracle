using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Configuration.Overrides;
using Orleans.Persistence.Oracle.Providers;

namespace Orleans.Persistence.Oracle.Storage;

public static class OracleGrainStorageFactory<T> where T : DbContext
{
    public static OracleGrainStorage<T> Create(IServiceProvider service, string name)
    {
        var options = service.GetRequiredService<IOptionsMonitor<OracleGrainStorageOptions>>();

        return ActivatorUtilities.CreateInstance<OracleGrainStorage<T>>(service, name, options.Get(name), service.GetProviderClusterOptions(name));
    }
}