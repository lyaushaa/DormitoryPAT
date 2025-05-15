using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DormitoryPAT.Models
{
    public class DutyEducators
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DutyId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [ForeignKey("Employees")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(15)]
        public string ContactNumber { get; set; }

        [Required]
        [Column(TypeName = "ENUM('Floor2_4', 'Floor5_7')")]
        public EducatorFloor Floor { get; set; }

        // Навигационное свойство
        public Employees Employees { get; set; }
    }

    public enum EducatorFloor
    {
        [Display(Name = "Floor2_4")]
        Floor2_4,
        [Display(Name = "Floor5_7")]
        Floor5_7
    }
}
