using Microsoft.EntityFrameworkCore;
using Orleans.Configuration;
using Orleans.Oracle.Core;
using System.Net.NetworkInformation;
using System.Net.Sockets;



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


var builder = WebApplication.CreateBuilder(args);
var conn = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=192.168.68.81)(PORT=31521))(CONNECT_DATA=(SID=orcl)));Persist Security Info=True;User Id=c##cskhtan;Password=cskhEcoit123";
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

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

await app.RunAsync($"http://{bindAdress}:11001");
