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
    public class RepairRequestsContext : DbContext
    {
        public DbSet<RepairRequests> RepairRequests { get; set; }
        public RepairRequestsContext()
        {
            Database.EnsureCreated();
            RepairRequests.Load();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(Config.connection, Config.version);
        }
    }
}
