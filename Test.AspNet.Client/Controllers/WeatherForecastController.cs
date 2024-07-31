using Microsoft.AspNetCore.Mvc;
using TestGrain;

namespace Test.AspNet.Client.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        private readonly IClusterClient _client;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IClusterClient client)
        {
            _logger = logger;
            _client = client;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            var g = _client.GetGrain<IHelloGrain>(Guid.NewGuid());
            g.SayHello("hello");
            g.SaveColumn();
            var c = g.GetMyColumn();

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}