using Microsoft.EntityFrameworkCore;

namespace Orleans.Persistence.Oracle
{
    public class StorageContext : DbContext
    {
        public StorageContext(DbContextOptions<StorageContext> options) : base(options) { }
    }
}
