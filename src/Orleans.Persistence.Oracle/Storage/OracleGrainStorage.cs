using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Persistence.Oracle.Providers;
using Orleans.Runtime;
using Orleans.Storage;
using System.Reflection;

namespace Orleans.Persistence.Oracle.Storage;

public class OracleGrainStorage<TContext> : IGrainStorage, ILifecycleParticipant<ISiloLifecycle> where TContext : DbContext
{
    private readonly string _storageName;
    private readonly OracleGrainStorageOptions _options;
    private readonly ClusterOptions _clusterOptions;
    private readonly TContext _context;
    private readonly IList<Type> _tables;

    public OracleGrainStorage(string storageName, OracleGrainStorageOptions options, IOptions<ClusterOptions> clusterOptions, TContext context)
    {
        _options = options;
        _clusterOptions = clusterOptions.Value;
        _storageName = _clusterOptions.ServiceId + "_" + storageName;
        _context = context;
        _tables = options.Tables;
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
                await _context.CreateTableIfNotExistsAsync(item);
            }
        });
    }
    public async Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        var type = typeof(T);
        var itemsPop = type.GetProperty("Items");
        if (itemsPop != null)
        {
            var itemType = itemsPop.PropertyType.GetGenericArguments().FirstOrDefault();
            if (itemType != null)
            {
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

    public async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        var type = typeof(T);
        var itemsPop = type.GetProperty("Items");
        if (itemsPop != null)
        {
            var itemType = itemsPop.PropertyType.GetGenericArguments().FirstOrDefault();
            if (itemType != null)
            {
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

    public async Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
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
                    await _context.InsertOrUpdateAsync(grainId.GetGuidKey().ToString(), items, itemType);
                }
                else
                {
                    itemsPop.SetValue(grainState.State, new List<object>());
                }
            }
        }
        grainState.RecordExists = true;
    }

}