using Microsoft.EntityFrameworkCore;

namespace SiloTest
{
    public class Test2Context : DbContext
    {
        public Test2Context(DbContextOptions<Test2Context> options) : base(options) { }
        public IQueryable<dynamic> QueryDynamicTable(string sql)
        {
            return this.Set<dynamic>().FromSqlRaw(sql);
        }
    }
}
