using Microsoft.EntityFrameworkCore;

namespace Orleans.Clustering.Oracle
{
    public class ClustringContext : DbContext
    {
        public ClustringContext(DbContextOptions<ClustringContext> options) : base(options) { }
    }
}
