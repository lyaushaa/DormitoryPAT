using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace DormitoryPAT.Models
{
    public class Employees
    {
        [Key]
        [ForeignKey("Users")]
        public int EmployeeId { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public EmployeeRole EmployeeRole { get; set; }

        // Навигационные свойства
        public User User { get; set; }
        public ICollection<MasterEmployees> MasterSpecialties { get; set; }
        public ICollection<RepairRequests> AssignedRepairRequests { get; set; }
        public ICollection<DutyEducators> DutyEducatorShifts { get; set; }
    }

    public enum EmployeeRole
    {
        Мастер,
        Воспитатель,
        [Display(Name = "Дежурный воспитатель")]
        Дежурный_воспитатель,
        Администратор
    }
}
