using Microsoft.EntityFrameworkCore;

namespace Orleans.Persistence.Oracle
{
    public class OraDbContext : DbContext
    {
        public OraDbContext(DbContextOptions<OraDbContext> options) : base(options) { }
    }
}
