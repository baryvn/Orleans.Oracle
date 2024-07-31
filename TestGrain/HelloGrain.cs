using Microsoft.Extensions.Logging;
using Orleans.Runtime;
namespace TestGrain
{
    public class HelloGrain : Grain, IHelloGrain
    {
        private readonly ILogger _logger;

        private readonly IPersistentState<TestModel> _test;
        public HelloGrain(ILogger<HelloGrain> logger, [PersistentState("test", "Test1Context")] IPersistentState<TestModel> test)
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
}
