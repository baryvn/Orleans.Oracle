using System.ComponentModel;


namespace TestGrain
{
    [Description("TEST_TABLE")]

    [GenerateSerializer]
    public class TestModel : BaseEntity
    {
        [Description("VARCHAR2(50)")]
        [Id(0)]
        public string MYCOLUM { get; set; }
    }
}
