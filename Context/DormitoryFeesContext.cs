using DormitoryPAT.Context.Database;
using DormitoryPAT.Models;
using Microsoft.EntityFrameworkCore;

namespace DormitoryPAT.Context
{
    public class DormitoryFeesContext : DbContext
    {
        public DbSet<DormitoryFees> DormitoryFees { get; set; }
        public DormitoryFeesContext()
        {
            Database.EnsureCreated();
            DormitoryFees.Load();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(Config.connection, Config.version);
        }
    }
}
