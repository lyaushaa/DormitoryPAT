using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DormitoryPAT.Context.Database
{
    public class Config
    {
        public static readonly string connection = "server=127.0.0.1;database=DormitoryPAT;uid=root;pwd=;";
        public static readonly MySqlServerVersion version = new MySqlServerVersion(new Version(8, 0, 11));
    }
}
