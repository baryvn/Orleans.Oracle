using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace TestGrain
{

    [GenerateSerializer]
    public class BaseEntity
    {
        [Description("VARCHAR2(128)")]
        [Id(0)]
        [Key]
        public string ID { get; set; } = Guid.NewGuid().ToString();

    }
}
