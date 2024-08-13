using Microsoft.EntityFrameworkCore;

namespace SiloTest
{
    public class Test1Context : DbContext
    {
        public Test1Context(DbContextOptions<Test1Context> options) : base(options) { }
    }
}
