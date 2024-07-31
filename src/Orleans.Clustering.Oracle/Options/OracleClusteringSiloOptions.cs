namespace Orleans.Configuration
{
    /// <summary>
    /// Option to configure RqliteMembership
    /// </summary>
    public class OracleClusteringSiloOptions
    {
        /// <summary>
        /// Connection string for Rqlite storage
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;
    }
}
