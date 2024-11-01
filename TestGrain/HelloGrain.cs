using Microsoft.Extensions.Logging;
using Orleans.Oracle.Core;
using Orleans.Timers;
namespace TestGrain
{
    public class HelloGrain : Grain, IHelloGrain,IRemindable
{
    private readonly ILogger _logger;

    private readonly IReminderRegistry _reminderRegistry;
    private readonly IPersistentState<BaseState<TestModel>> _test;

    private IGrainReminder? _rTest;
    private bool _taskDone = false;

    public HelloGrain(ILogger<HelloGrain> logger,            IReminderRegistry reminderRegistry,[PersistentState("test", "Storage")] IPersistentState<BaseState<TestModel>> test)
    {
        _logger = logger;
        _test = test;
        _reminderRegistry = reminderRegistry;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(_test.ReadStateAsync());
    }


    public async Task<string> GetCount()
    {
        return _test.State.Items.Count.ToString();
    }

    public async Task AddItem(TestModel model)
    {
        // items is a list
        _test.State.Items.Add(model);        
        await _test.WriteStateAsync();
    }
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        try
        {
            if(reminderName == "TEST_REMIDER")
            {
                // Excute task
                if(_taskDone)
                {
                    if (_rTest == null)
                    {
                        _rTest = await _reminderRegistry.GetReminder(GrainContext.GrainId, "TEST_REMIDER");
                    }
                    if (_rTest != null)
                        await _reminderRegistry.UnregisterReminder(GrainContext.GrainId, _rTest);
                }
            }
        }
        catch (Exception ex)
        {
            //log
        }
    }
    public async Task RegisterRemider()
    {
        if (_rTest == null)
        {
            _rTest = await _reminderRegistry.GetReminder(GrainContext.GrainId,"TEST_REMIDER");
        }
        if (_rTest == null)
        {
            _rTest = await _reminderRegistry.RegisterOrUpdateReminder(
            callingGrainId: GrainContext.GrainId,
            reminderName: "TEST_REMIDER",
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromMinutes(1));
        }
    }

}
}
