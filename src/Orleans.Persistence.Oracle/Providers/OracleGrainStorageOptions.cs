using Microsoft.EntityFrameworkCore;
using Orleans.Storage;

namespace Orleans.Persistence.Oracle.Providers;

public class OracleGrainStorageOptions : IStorageProviderSerializerOptions
{
    public IList<Type> Tables { get; set; } = new List<Type>();
    public required IGrainStorageSerializer GrainStorageSerializer { get; set; }
}