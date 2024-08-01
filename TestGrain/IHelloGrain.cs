namespace TestGrain
{
    public interface IHelloGrain : IGrainWithGuidKey
    {
        Task<string> GetCount();

        Task AddItem(TestModel model);
    }
}
