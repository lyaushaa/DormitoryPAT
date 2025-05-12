using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

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
        [Column(TypeName = "ENUM('Мастер', 'Воспитатель', 'Дежурный_воспитатель', 'Администратор')")]
        public EmployeeRole EmployeeRole { get; set; }

        // Навигационные свойства
        public Users Users { get; set; }
        public ICollection<MasterEmployees> MasterSpecialties { get; set; }
        public ICollection<RepairRequests> AssignedRepairRequests { get; set; }
        public ICollection<DutyEducators> DutyEducatorShifts { get; set; }
    }

    public enum EmployeeRole
    {
        [Display(Name = "Мастер")]
        Мастер,

        [Display(Name = "Воспитатель")]
        Воспитатель,

        [Display(Name = "Дежурный_воспитатель")]
        Дежурный_воспитатель,

        [Display(Name = "Администратор")]
        Администратор
    }
}
