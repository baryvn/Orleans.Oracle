using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Persistence.Oracle.Providers;
using Orleans.Oracle.Core;
using Orleans.Runtime;
using Orleans.Storage;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Orleans.Persistence.Oracle.Storage;

public class OracleGrainStorage : IGrainStorage, ILifecycleParticipant<ISiloLifecycle>
{
    private readonly string _storageName;
    private readonly OracleGrainStorageOptions _options;
    private readonly ClusterOptions _clusterOptions;
    private readonly IList<Type> _tables;
    private readonly ILogger<OracleGrainStorage> _logger;
    private readonly IServiceProvider _provider;
    private string? _connectionString = string.Empty;

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
                    var _context = _provider.CreateAsyncScope().ServiceProvider.GetService<OracleDbContext>();
                    if (_context == null)
                    {
                        throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                    }

                    await _context.CreateTableIfNotExistsAsync(item);
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
                    using (var scope = _provider.CreateAsyncScope())
                    using (var _Wcontext = scope.ServiceProvider.GetService<OracleDbContext>())
                    {

                        if (_Wcontext == null)
                        {
                            throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                        }
                        await _Wcontext.DeleteEntityAsync(grainId.GetGuidKey(), itemType);
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
        finally
        {
        }
    }

    public async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        try
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                using (var scope = _provider.CreateAsyncScope())
                using (var _Wcontext = scope.ServiceProvider.GetService<OracleDbContext>())
                {

                    if (_Wcontext == null)
                    {
                        throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                    }
                    _connectionString = _Wcontext.Database.GetConnectionString();
                }
                if (string.IsNullOrEmpty(_connectionString))
                {
                    throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                }
            }

            var type = typeof(T);
            var itemsPop = type.GetProperty("Items");
            if (itemsPop != null)
            {
                var itemType = itemsPop.PropertyType.GetGenericArguments().FirstOrDefault();
                if (itemType != null)
                {
                    var result = await _connectionString.GetEntityByIdAsync(grainId.GetGuidKey().ToString(), itemType);
                    if (result != null)
                    {
                        Type listType = typeof(List<>).MakeGenericType(itemType);
                        MethodInfo addMethod = listType.GetMethod("Add");

                        var listInstance = Activator.CreateInstance(listType);
                        foreach (var item in result)
                        {
                            addMethod.Invoke(listInstance, [item]);
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
            if (string.IsNullOrEmpty(_connectionString))
            {
                using (var scope = _provider.CreateAsyncScope())
                using (var _Wcontext = scope.ServiceProvider.GetService<OracleDbContext>())
                {

                    if (_Wcontext == null)
                    {
                        throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                    }
                    _connectionString = _Wcontext.Database.GetConnectionString();
                }
                if (string.IsNullOrEmpty(_connectionString))
                {
                    throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                }
            }
            var type = typeof(T);
            var itemsPop = type.GetProperty("Items");
            if (itemsPop != null)
            {
                var itemType = itemsPop.PropertyType.GetGenericArguments().FirstOrDefault();
                if (itemType != null)
                {
                    var states = itemsPop.GetValue(grainState.State) as IEnumerable<object>;
                    if (states != null)
                    {
                        using (var scope = _provider.CreateAsyncScope())
                        {
                            var result = new List<object>();
                            using (var _Rcontext = scope.ServiceProvider.GetService<OracleDbContext>())
                            {
                                if (_Rcontext == null)
                                {
                                    throw new Exception("Lỗi kết nối cơ sở dữ liệu");
                                }
                                result = await _connectionString.GetEntityByIdAsync(grainId.GetGuidKey().ToString(), itemType);
                                if (result != null)
                                {
                                    var inserts = new List<object>();
                                    var updates = new List<object>();
                                    var deletes = new List<object>();
                                    foreach (var s in states)
                                    {
                                        var r = result.Find(r => Extentions.CompareObjectKeys(itemType, s, r));
                                        if (r != null)
                                        {
                                            if (!Extentions.CompareObjects(itemType, r, s))
                                                updates.Add(s);
                                        }
                                        else
                                            inserts.Add(s);
                                    }
                                    foreach (var r in result)
                                    {
                                        if (!states.Any(s => Extentions.CompareObjectKeys(itemType, s, r)))
                                            deletes.Add(r);
                                    }
                                    using (var trans = _Rcontext.Database.BeginTransaction())
                                    {
                                        try
                                        {
                                            await _Rcontext.InsertAsync(inserts, itemType);
                                            await _Rcontext.UpdateAsync(updates, itemType);
                                            await _Rcontext.DeleteAsync(deletes, itemType);
                                            await trans.CommitAsync();
                                        }
                                        catch (Exception ex)
                                        {
                                            await trans.RollbackAsync();
                                            throw new Exception(ex.Message);
                                        }
                                    }
                                }
                            }

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