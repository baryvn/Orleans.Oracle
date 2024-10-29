using Microsoft.EntityFrameworkCore;

namespace Orleans.Oracle.Core
{
    public class OracleDbContext : DbContext
    {
        public OracleDbContext(DbContextOptions<OracleDbContext> options) : base(options)
        {
        }
    }
}
