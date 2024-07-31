using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Persistence.Oracle.Providers;
using Orleans.Runtime;
using Orleans.Storage;

namespace Orleans.Persistence.Oracle.Storage;

public class OracleGrainStorage<T> : IGrainStorage, ILifecycleParticipant<ISiloLifecycle> where T : DbContext
{
    private readonly string _storageName;
    private readonly OracleGrainStorageOptions _options;
    private readonly ClusterOptions _clusterOptions;
    private readonly T _context;
    private readonly IList<Type> _tables;

    public OracleGrainStorage(string storageName, OracleGrainStorageOptions options, IOptions<ClusterOptions> clusterOptions, T context)
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
        var result = await _context.GetEntityByIdAsync<T>(grainId.GetGuidKey().ToString());
        if (result != null)
        {


            var type = typeof(T);
            var etagPop = type.GetProperties().FirstOrDefault(p => p.Name == "ETAG");
            if (etagPop != null)
            {
                var etag = etagPop.GetValue(result);
                if (etag == null || !etag.Equals(grainState.ETag))
                {
                    throw new InconsistentStateException("ETag mismatch.");
                }

                await _context.DeleteEntityAsync<T>(grainId.GetGuidKey().ToString());
                grainState.ETag = null;
                grainState.State = Activator.CreateInstance<T>()!;
                grainState.RecordExists = false;
            }
        }
    }

    public async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {

        var result = await _context.GetEntityByIdAsync<T>(grainId.GetGuidKey().ToString());
        if (result != null)
        {
            var type = typeof(T);
            var etagPop = type.GetProperties().FirstOrDefault(p => p.Name == "ETAG");
            if (etagPop != null)
            {
                var etag = etagPop.GetValue(result);
                if (etag == null || !etag.Equals(grainState.ETag))
                {
                    throw new InconsistentStateException("ETag mismatch.");
                }
                grainState.ETag = etag.ToString();
            }
            grainState.State = result != null ? result : Activator.CreateInstance<T>();
            grainState.RecordExists = true;
        }
        grainState.State = Activator.CreateInstance<T>()!;
        return;
    }

    public async Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        var result = await _context.GetEntityByIdAsync<T>(grainId.GetGuidKey().ToString());
        if (result != null)
        {
            var type = typeof(T);
            var etagPop = type.GetProperties().FirstOrDefault(p => p.Name == "ETAG");
            if (etagPop != null)
            {
                var etag = etagPop.GetValue(result);
                if (etag == null || !etag.Equals(grainState.ETag))
                {
                    throw new InconsistentStateException("ETag mismatch.");
                }
                grainState.ETag = Guid.NewGuid().ToString();
                etagPop.SetValue(result, grainState.ETag);
            }
            await _context.UpdateEntityAsync<T>(result);
        }
        else
        {
            result = Activator.CreateInstance<T>();
            var type = typeof(T);
            var key = grainId.GetGuidKey().ToString();
            var etagPop = type.GetProperties().FirstOrDefault(p => p.Name == "ETAG");
            if (etagPop != null)
            {
                grainState.ETag = key;
                etagPop.SetValue(result, grainState.ETag);
            }
            var idPop = type.GetProperties().FirstOrDefault(p => p.Name == "ID");
            if (idPop != null)
            {
                idPop.SetValue(result,key);
            }
            await _context.InsertEntityAsync<T>(grainState.State);
        }
        grainState.RecordExists = true;
    }
}