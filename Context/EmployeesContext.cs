using DormitoryPAT.Context.Database;
using DormitoryPAT.Models;
using Microsoft.EntityFrameworkCore;

namespace DormitoryPAT.Context
{
    public class EmployeesContext : DbContext
    {
        public DbSet<Employees> Employees { get; set; }
        public EmployeesContext()
        {
            Database.EnsureCreated();
            Employees.Load();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(Config.connection, Config.version);
        }
    }
}
