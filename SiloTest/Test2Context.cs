using Microsoft.EntityFrameworkCore;

namespace SiloTest
{
    public class Test2Context : DbContext
    {
        public Test2Context(DbContextOptions<Test2Context> options) : base(options) { }
    }
}
