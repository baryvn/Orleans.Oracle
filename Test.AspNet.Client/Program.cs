using Orleans.Configuration;
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
        option.ConnectionString = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle19c.intemi.vn)(PORT=1521))(CONNECT_DATA=(SID=orcl)));Persist Security Info=True;User Id=c##clusterapp;Password=intemi2019";
    });
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

await app.RunAsync("http://192.168.68.41:11001");
