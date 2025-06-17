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
    public class DutyScheduleContext : DbContext
    {
        public DbSet<DutySchedule> DutySchedule { get; set; }
        public DbSet<RoomCleanlinessLinks> RoomCleanlinessLinks { get; set; }
        public DutyScheduleContext()
        {
            Database.EnsureCreated();
            DutySchedule.Load();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(Config.connection, Config.version);
        }
    }
}
