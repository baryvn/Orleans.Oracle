namespace TestGrain
{
    public interface IHelloGrain : IGrainWithGuidKey
    {
        ValueTask<string> SayHello(string greeting);
        Task<string> GetPolicy();

        void SavePolicy();
    }
}
