
# Orleans Oracle Providers
[Orleans](https://github.com/dotnet/orleans) is a framework that provides a straight-forward approach to building distributed high-scale computing applications, without the need to learn and apply complex concurrency or other scaling patterns. 


## **Orleans.Oracle** 
is a package that use Oracle as a backend for Orleans providers like Cluster Membership, Grain State storage. 

# Installation 
Nuget Packages are provided:
- Orleans.Oracle.Core
- Orleans.Clustering.Oracle
- Orleans.Persistence.Oracle
- Orleans.Reminders.Oracle

## Note
>In development environment sometimes you will have to delete member in cluster's Member table. the reason for this issue is when you suddenly stop the application while running test or debug orleans can't update the state down to member table properly and will show error when starting cluster

## Silo
```

IHostBuilder builder = Host.CreateDefaultBuilder(args)
    .UseOrleans(silo =>
    {
        silo.Configure<ClusterOptions>(options =>
        {
            options.ClusterId = "ORLEANS_ORACLE_DC";
            options.ServiceId = "ORLEANS_ORACLE";

        });

        // Add Oracle DbContext, this db context is used in, Clustering,GrainStorage and Reminder
        var conn = "******************";
        silo.Services.AddDbContext<OracleDbContext>(options => options.UseOracle(conn, o =>
        {
            o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
        }), ServiceLifetime.Scoped);]
        // Add clustering
        silo.UseOracleClustering();

        // Add Persitend storage
        silo.AddOracleGrainStorage("Storage", option =>
        {
            option.Tables = new List<Type> { typeof(TestModel) };
        });

        // Add Reminder
        silo.UseOracleReminder();

        
        silo.ConfigureLogging(logging => logging.AddConsole());

        silo.ConfigureEndpoints(
            siloPort: 11111,
            gatewayPort: 30001,
            advertisedIP: IPAddress.Parse(bindAdress),
            listenOnAnyHostAddress: true
            );

        silo.Configure<ClusterMembershipOptions>(options =>
        {
            options.EnableIndirectProbes = true;
            options.UseLivenessGossip = true;
        });
    })
    .UseConsoleLifetime();

using IHost host = builder.Build();

await host.RunAsync();
```
## Client 
```

var builder = WebApplication.CreateBuilder(args);
var conn = "****************";
builder.Services.AddDbContext<OracleDbContext>(options => options.UseOracle(conn, o =>
{
    o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
}), ServiceLifetime.Scoped);

builder.Host.UseOrleansClient(client =>
{
    client.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "ORLEANS_ORACLE_DC";
        options.ServiceId = "ORLEANS_ORACLE";
    });
    client.UseOracleClustering();
});

```


## Use Persistence
- BaseEntity is require 
- property name is uppercase 
- [Description("TEST_TABLE")] of class is table name
-  [Description("VARCHAR2(50)")] of properties is oracle data type
-  [Key] is GrainKey type GuidKey
-  [Key] and [GroupKey] of properties is set this properties is primarykey in oracle
### BaseEntity
```
[GenerateSerializer]
public class BaseEntity
{
    [Description("VARCHAR2(128)")]
    [Id(0)]
    [Key]
    public string ID { get; set; } = Guid.NewGuid().ToString();
}
```
### Table
```
[Description("TEST_TABLE")]
[GenerateSerializer]
public class TestModel : BaseEntity
{
    [Description("VARCHAR2(128)")]
    [Id(1)]
    [GroupKey]
    public string FORENKEY { get; set; } = Guid.NewGuid().ToString();

    [Description("VARCHAR2(50)")]
    [Id(0)]
    public string MYCOLUM { get; set; }
}
```

### interface grain
```
public interface IHelloGrain : IGrainWithGuidKey
{
    ValueTask<string> SayHello(string greeting);
    Task<string> GetMyColumn();

    void SaveColumn();
}
```
### impliment grain
```
using Orleans.Persistence.Oracle.States;
public class HelloGrain : Grain, IHelloGrain
{
    private readonly ILogger _logger;

    private readonly IPersistentState<BaseState<TestModel>> _test;
    public HelloGrain(ILogger<HelloGrain> logger, [PersistentState("test", "Storage")] IPersistentState<BaseState<TestModel>> test)
    {
        _logger = logger;
        _test = test;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(_test.ReadStateAsync());
    }


    public async Task<string> GetCount()
    {
        return _test.State.Items.Count.ToString();
    }

    public async Task AddItem(TestModel model)
    {
        // items is a list
        _test.State.Items.Add(model);        
        await _test.WriteStateAsync();
    }
}

```

