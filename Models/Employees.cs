using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DormitoryPAT.Models
{
    public class Employees
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long EmployeeId { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        [Required]
        public string FIO { get; set; }

        [Required]
        [Column(TypeName = "ENUM('Мастер', 'Воспитатель', 'Заведующий_общежитием', 'Администратор')")]
        public EmployeeRole EmployeeRole { get; set; }

        [Required]
        public string Login { get; set; }

        [Required]
        public string Password { get; set; }

        // Навигационные свойства
        public ICollection<RepairRequests> AssignedRepairRequests { get; set; }        
        [InverseProperty("Reviewer")]
        public ICollection<Complaints> ReviewedComplaints { get; set; }

        [NotMapped]
        public string EmployeeRoleDisplay => EmployeeRole switch
        {
            EmployeeRole.Мастер => "Мастер",
            EmployeeRole.Воспитатель => "Воспитатель",
            EmployeeRole.Заведующий_общежитием => "Заведующий общежитием",
            EmployeeRole.Администратор => "Администратор",
            _ => EmployeeRole.ToString()
        };
    }

    public enum EmployeeRole
    {
        [Display(Name = "Мастер")]
        Мастер,

        [Display(Name = "Воспитатель")]
        Воспитатель,

        [Display(Name = "Заведующий_общежитием")]
        Заведующий_общежитием,

        [Display(Name = "Администратор")]
        Администратор
    }
}
