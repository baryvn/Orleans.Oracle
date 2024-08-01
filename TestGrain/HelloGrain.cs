using Microsoft.Extensions.Logging;
using Orleans.Persistence.Oracle.States;
namespace TestGrain
{
    public class HelloGrain : Grain, IHelloGrain
    {
        private readonly ILogger _logger;

        private readonly IPersistentState<BaseState<TestModel>> _test;
        public HelloGrain(ILogger<HelloGrain> logger, [PersistentState("test", "Test1Context")] IPersistentState<BaseState<TestModel>> test)
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
        public async Task<string> GetCount()
        {
            await _test.ReadStateAsync();
            return _test.State.Items.Count.ToString();
        }

        public async Task AddItem(TestModel model)
        {
            _test.State.Items = [model,];
            await _test.WriteStateAsync();
        }


    }
}
