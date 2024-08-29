using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Persistence.Oracle.Providers;
using Orleans.Runtime;
using Orleans.Storage;
using System.Reflection;

namespace Orleans.Persistence.Oracle.Storage;

public class OracleGrainStorage : IGrainStorage, ILifecycleParticipant<ISiloLifecycle>
{
    private readonly string _storageName;
    private readonly OracleGrainStorageOptions _options;
    private readonly ClusterOptions _clusterOptions;
    private readonly IList<Type> _tables;
    private readonly ILogger<OracleGrainStorage> _logger;
    private readonly IServiceProvider _provider;

    public OracleGrainStorage(string storageName, OracleGrainStorageOptions options, IServiceProvider provider, IOptions<ClusterOptions> clusterOptions, ILogger<OracleGrainStorage> logger)
    {
        _options = options;
        _clusterOptions = clusterOptions.Value;
        _storageName = _clusterOptions.ServiceId + "_" + storageName;
        _tables = options.Tables;
        _logger = logger;
        _provider = provider;
    }


    public void Participate(ISiloLifecycle observer)
    {
        observer.Subscribe(
        observerName: OptionFormattingUtilities.Name<OracleGrainStorageOptions>(_storageName),
        stage: ServiceLifecycleStage.ApplicationServices,
        onStart: async (ct) =>
        {
            foreach (var item in _tables)
            {
                try
                {
                    var optionsBuilder = new DbContextOptionsBuilder<StorageContext>();
                    optionsBuilder.UseOracle(_options.ConnectionString);
                    using (var _context = new StorageContext(optionsBuilder.Options))
                    {
                        await _context.CreateTableIfNotExistsAsync(item);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
        });
    }
    public async Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        try
        {
            var type = typeof(T);
            var itemsPop = type.GetProperty("Items");
            if (itemsPop != null)
            {
                var itemType = itemsPop.PropertyType.GetGenericArguments().FirstOrDefault();
                if (itemType != null)
                {
                    var _context = _provider.GetService<StorageContext>();
                    if (_context == null)
                    {
                        throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                    }

                    var result = await _context.GetEntityByIdAsync(grainId.GetGuidKey().ToString(), itemType);
                    if (result != null && result.Any())
                    {
                        await _context.DeleteEntityAsync(grainId.GetGuidKey().ToString(), itemType);
                        grainState.State = Activator.CreateInstance<T>()!;
                        grainState.RecordExists = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        try
        {
            var type = typeof(T);
            var itemsPop = type.GetProperty("Items");
            if (itemsPop != null)
            {
                var itemType = itemsPop.PropertyType.GetGenericArguments().FirstOrDefault();
                if (itemType != null)
                {


                    var _context = _provider.GetService<StorageContext>();
                    if (_context == null)
                    {
                        throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                    }

                    var result = await _context.GetEntityByIdAsync(grainId.GetGuidKey().ToString(), itemType);
                    if (result != null)
                    {
                        Type listType = typeof(List<>).MakeGenericType(itemType);
                        MethodInfo addMethod = listType.GetMethod("Add");

                        var listInstance = Activator.CreateInstance(listType);
                        foreach (var item in result)
                        {
                            var itemVal = Extentions.ConvertToTestModel(item, itemType);
                            addMethod.Invoke(listInstance, new object[] { itemVal });

                        }
                        var state = Activator.CreateInstance<T>();
                        itemsPop.SetValue(state, listInstance);
                        grainState.State = state;
                        grainState.RecordExists = true;
                    }
                    else
                    {
                        grainState.State = Activator.CreateInstance<T>()!;
                    }
                }
                else
                {
                    grainState.State = Activator.CreateInstance<T>()!;
                }

            }
            else
            {
                grainState.State = Activator.CreateInstance<T>()!;
            }
            return;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        try
        {
            var type = typeof(T);
            var itemsPop = type.GetProperty("Items");
            if (itemsPop != null)
            {
                var itemType = itemsPop.PropertyType.GetGenericArguments().FirstOrDefault();
                if (itemType != null)
                {
                    var items = itemsPop.GetValue(grainState.State) as IEnumerable<object>;
                    if (items != null)
                    {
                        var _context = _provider.GetService<StorageContext>();
                        if (_context == null)
                        {
                            throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                        }

                        var result = await _context.GetEntityByIdAsync(grainId.GetGuidKey().ToString(), itemType);
                        if (result != null)
                        {
                            await _context.InsertOrUpdateAsync(grainId.GetGuidKey().ToString(), items, itemType);
                        }
                    }
                    else
                    {
                        itemsPop.SetValue(grainState.State, new List<object>());
                    }
                }
            }
            grainState.RecordExists = true;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

}