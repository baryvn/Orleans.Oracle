
# Orleans Oracle Providers
[Orleans](https://github.com/dotnet/orleans) is a framework that provides a straight-forward approach to building distributed high-scale computing applications, without the need to learn and apply complex concurrency or other scaling patterns. 


## **Orleans.Oracle** 
is a package that use Oracle as a backend for Orleans providers like Cluster Membership, Grain State storage and Reminders. 

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
            option.ConnectionString = "";
        });
        silo.AddOracleGrainStorage("HelloGrain",option =>
        {
            option.ConnectionString = "";
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
## Use Persistence
- BaseEntity is require 
- property name is uppercase
```
namespace TestGrain
{

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
}
```
### impliment grain
- [Description("TEST_TABLE")] is table name
-  [Description("VARCHAR2(50)")] is oracle data type
```
namespace TestGrain
{
    [Description("TEST_TABLE")]

    [GenerateSerializer]
    public class TestModel : BaseEntity
    {
        [Description("VARCHAR2(50)")]
        [Id(0)]
        public string MYCOLUM { get; set; }
    }
}

```
### interface grain
```
namespace TestGrain
{
    public interface IHelloGrain : IGrainWithGuidKey
    {
        ValueTask<string> SayHello(string greeting);
        Task<string> GetPolicy();

        void SavePolicy();
    }
}
 public class HelloGrain : Grain, IHelloGrain
 {
     private readonly ILogger _logger;

     private readonly IPersistentState<TestModel> _policy;
     public HelloGrain(ILogger<HelloGrain> logger, [PersistentState("policy", "HelloGrain")] IPersistentState<TestModel> policy)
     {
         _logger = logger;
         _policy = policy;
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

     public async Task<string> GetPolicy()
     {
         await _policy.ReadStateAsync();
         return _policy.State.MYCOLUM;
     }

     public async void SavePolicy()
     {
         _policy.State.MYCOLUM = "test";
         await _policy.WriteStateAsync();
     }
 }

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
