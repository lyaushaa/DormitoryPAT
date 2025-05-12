using DormitoryPAT.Context.Database;
using DormitoryPAT.Models;
using Microsoft.EntityFrameworkCore;

namespace DormitoryPAT.Context
{
    public class PhoneDirectoryContext : DbContext
    {
        public DbSet<PhoneDirectory> PhoneDirectory { get; set; }
        public PhoneDirectoryContext()
        {
            Database.EnsureCreated();
            PhoneDirectory.Load();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(Config.connection, Config.version);
        }
    }
}
