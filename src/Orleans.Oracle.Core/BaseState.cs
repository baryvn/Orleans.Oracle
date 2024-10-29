
namespace Orleans.Oracle.Core
{
    [GenerateSerializer]
    public class BaseState<T>
    {
        [Id(0)]
        public List<T> Items { get; set; } = new List<T>();
    }
}
