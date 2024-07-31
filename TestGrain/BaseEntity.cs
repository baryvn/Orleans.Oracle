using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace TestGrain
{

    [GenerateSerializer]
    public class BaseEntity
    {
        [Description("CHAR(36)")]
        [Id(0)]
        [Key]
        public string ID { get; set; } = Guid.NewGuid().ToString();
        [Description("CHAR(36)")]
        [Id(1)]
        public string ETAG { get; set; } = string.Empty;

    }
}
