using DormitoryPAT.Context.Database;
using DormitoryPAT.Models;
using Microsoft.EntityFrameworkCore;

namespace DormitoryPAT.Context
{
    public class MasterSpecialtiesContext : DbContext
    {
        public DbSet<MasterSpecialties> MasterSpecialties { get; set; }
        public MasterSpecialtiesContext()
        {
            Database.EnsureCreated();
            MasterSpecialties.Load();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(Config.connection, Config.version);
        }
    }
}
