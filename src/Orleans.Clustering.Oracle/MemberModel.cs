using Orleans.Runtime;
using System.ComponentModel;

namespace Orleans.Clustering.Oracle
{
    public class MemberModel
    {
        public string SiloAddress { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public DateTime IAmAliveTime { get; set; } = DateTime.MinValue;
        public int Status { get; set; } 

    }
    
}
