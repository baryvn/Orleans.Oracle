using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using Orleans.Persistence.Oracle.Hosting;
using SiloTest;
using System.Net;
using TestGrain;

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
            option.ConnectionString = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle19c.intemi.vn)(PORT=1521))(CONNECT_DATA=(SID=orcl)));Persist Security Info=True;User Id=c##clusterapp;Password=intemi2019";
        });
        silo.AddOracleGrainStorage<Test1Context>("Test1Context", option =>
        {
            option.ConnectionString = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle19c.intemi.vn)(PORT=1521))(CONNECT_DATA=(SID=orcl)));Persist Security Info=True;User Id=c##cskh;Password=intemi2019";
            option.Tables = new List<Type> { typeof(TestModel) };
        });
        silo.AddOracleGrainStorage<Test2Context>("Test2Context", option =>
        {
            option.ConnectionString = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle19c.intemi.vn)(PORT=1521))(CONNECT_DATA=(SID=orcl)));Persist Security Info=True;User Id=c##cskh;Password=intemi2019";
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