using Orleans.Storage;

namespace Orleans.Persistence.Oracle.Providers;

public class OracleGrainStorageOptions : IStorageProviderSerializerOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public IList<Type> Tables { get; set; } = new List<Type>();
    public required IGrainStorageSerializer GrainStorageSerializer { get; set; }
}