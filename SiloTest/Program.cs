using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using Orleans.Oracle.Core;
using Orleans.Persistence.Oracle.Hosting;
using Orleans.Reminders.Oracle;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TestGrain;

var bindAdress = string.Empty;

foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
{
    if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
        ni.OperationalStatus == OperationalStatus.Up)
    {
        if (!string.IsNullOrEmpty(bindAdress)) break;
        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
        {
            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                bindAdress = ip.Address.ToString();
                break;
            }
        }
    }
}



IHostBuilder builder = Host.CreateDefaultBuilder(args)
    .UseOrleans(silo =>
    {
        silo.Configure<ClusterOptions>(options =>
        {
            options.ClusterId = "ORLEANS_ORACLE_DC";
            options.ServiceId = "ORLEANS_ORACLE";

        });
        var conn = "******************";
        silo.Services.AddDbContext<OracleDbContext>(options => options.UseOracle(conn, o =>
        {
            o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
        }), ServiceLifetime.Scoped);

        silo.UseOracleClustering();
        silo.AddOracleGrainStorage("Storage", option =>
        {
            option.Tables = new List<Type> { typeof(TestModel) };
        });
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