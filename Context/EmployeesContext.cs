using DormitoryPAT.Context.Database;
using DormitoryPAT.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
