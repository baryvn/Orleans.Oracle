using Microsoft.Extensions.Logging;
using Orleans.Runtime;
namespace TestGrain
{
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
}
