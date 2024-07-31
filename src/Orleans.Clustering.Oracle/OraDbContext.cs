using Microsoft.EntityFrameworkCore;

namespace Orleans.Clustering.Oracle
{
    public class OraDbContext : DbContext
    {
        public OraDbContext(DbContextOptions<OraDbContext> options) : base(options) { }
    }
}
