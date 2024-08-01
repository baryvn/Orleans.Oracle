namespace TestGrain
{
    public interface IHelloGrain : IGrainWithGuidKey
    {
        ValueTask<string> SayHello(string greeting);
        Task<string> GetCount();

        Task AddItem(TestModel model);
    }
}
