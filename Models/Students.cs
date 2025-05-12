using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DormitoryPAT.Models
{
    public class Students
    {
        [Key]
        [ForeignKey("Users")]
        public int StudentId { get; set; }

        [Required]
        public int Floor { get; set; }

        [Required]
        public int Room { get; set; }

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Column(TypeName = "ENUM('Студент', 'Староста этажа', 'Председатель общежития')")]
        public StudentRole StudentRole { get; set; } = StudentRole.Студент;

        // Навигационное свойство
        public Users Users { get; set; }
    }

    public enum StudentRole
    {
        [Display(Name = "Студент")]
        Студент,

        [Display(Name = "Староста_этажа")]
        Староста_этажа,

        [Display(Name = "Председатель_общежития")]
        Председатель_общежития
    }
}
