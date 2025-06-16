using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DormitoryPAT.Models
{
    public class Students
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long StudentId { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        [Required]
        public string FIO { get; set; }

        [Required]
        public string Group { get; set; }

        [Required]
        public int Floor { get; set; }

        [Required]
        public int Room { get; set; }

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Column(TypeName = "ENUM('Студент', 'Староста_этажа', 'Председатель_Студенческого_совета_общежития')")]
        public StudentRole StudentRole { get; set; }

        public DateTime? CheckInDate { get; set; }

        public DateTime? CheckOutDate { get; set; }

        public long? TelegramId { get; set; }

        [NotMapped]
        public string StudentsRoleDisplay => StudentRole switch
        {
            StudentRole.Студент => "Студент",
            StudentRole.Староста_этажа => "Староста этажа",
            StudentRole.Председатель_Студенческого_совета_общежития => "Председатель Студенческого совета общежития",
            _ => StudentRole.ToString()
        };
    }

    public enum StudentRole
    {
        [Display(Name = "Студент")]
        Студент,

        [Display(Name = "Староста_этажа")]
        Староста_этажа,

        [Display(Name = "Председатель_Студенческого_совета_общежития")]
        Председатель_Студенческого_совета_общежития
    }
}
