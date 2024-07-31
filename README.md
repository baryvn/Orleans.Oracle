
# Orleans Oracle Providers
[Orleans](https://github.com/dotnet/orleans) is a framework that provides a straight-forward approach to building distributed high-scale computing applications, without the need to learn and apply complex concurrency or other scaling patterns. 


## **Orleans.Oracle** 
is a package that use Oracle as a backend for Orleans providers like Cluster Membership, Grain State storage. 

# Installation 
Nuget Packages are provided:
- Orleans.Persistence.Oracle
- Orleans.Clustering.Oracle

## Silo
```

IHostBuilder builder = Host.CreateDefaultBuilder(args)
    .UseOrleans(silo =>
    {
        silo.Configure<ClusterOptions>(options =>
        {
            options.ClusterId = "DEV";
            options.ServiceId = "DEV";

        });
        silo.UseOracleClustering(option =>
        {
            option.ConnectionString = "your-connection-string";
        });
        silo.AddOracleGrainStorage<Test1Context>("Test1Context", option =>
        {
            option.ConnectionString = "your-connection-string";
            option.Tables = new List<Type> { typeof(TestModel) };
        });
        silo.AddOracleGrainStorage<Test2Context>("Test2Context", option =>
        {
            option.ConnectionString = "your-connection-string";
            option.Tables = new List<Type> { typeof(TestModel) };
        });
        silo.ConfigureLogging(logging => logging.AddConsole());

        silo.ConfigureEndpoints(
            siloPort: 11111,
            gatewayPort: 30001,
            advertisedIP: IPAddress.Parse("192.168.68.41"),
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
builder.Host.UseOrleansClient(client =>
{
    client.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "DEV";
        options.ServiceId = "DEV";

    });
    client.UseOracleClustering(option =>
    {
        option.ConnectionString = "";
    });
});

```

## Use Persistence
- BaseEntity is require 
- property name is uppercase 
- [Description("TEST_TABLE")] is table name
-  [Description("VARCHAR2(50)")] is oracle data type
```
[GenerateSerializer]
public class BaseEntity
{
    [Description("CHAR(36)")]
    [Id(0)]
    [Key]
    public string ID { get; set; } = Guid.NewGuid().ToString();
    [Description("CHAR(36)")]
    [Id(1)]
    public string ETAG { get; set; } = string.Empty;

}
[Description("TEST_TABLE")]

[GenerateSerializer]
public class TestModel : BaseEntity
{
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
public class HelloGrain : Grain, IHelloGrain
{
    private readonly ILogger _logger;

    private readonly IPersistentState<TestModel> _test;
    public HelloGrain(ILogger<HelloGrain> logger, [PersistentState("policy", "Test1Context")] IPersistentState<TestModel> test)
    {
        _logger = logger;
        _test = test;
    }

    ValueTask<string> IHelloGrain.SayHello(string greeting)
    {
        _logger.LogInformation("""
        SayHello message received: greeting = "{Greeting}"
        """,
            greeting);

        return ValueTask.FromResult($"""

        Client said: "{greeting}", so HelloGrain says: Hello!
        """);
    }

    public async Task<string> GetMyColumn()
    {
        await _test.ReadStateAsync();
        return _test.State.MYCOLUM;
    }

    public async void SaveColumn()
    {
        _test.State.MYCOLUM = "test";
        await _test.WriteStateAsync();
    }
}

```

