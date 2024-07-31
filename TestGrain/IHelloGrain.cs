namespace TestGrain
{
    public interface IHelloGrain : IGrainWithGuidKey
    {
        ValueTask<string> SayHello(string greeting);
        Task<string> GetMyColumn();

        void SaveColumn();
    }
}
