using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DormitoryPAT.Models
{
    public class MasterSpecialties
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SpecialtyId { get; set; }

        [Required]
        [Column(TypeName = "ENUM('Электрик', 'Плотник', 'Сантехник', 'Универсал')")]
        public SpecialtyName SpecialtyName { get; set; }

        // Навигационное свойство
        public ICollection<MasterEmployees> MasterEmployees { get; set; }
    }

    public enum SpecialtyName
    {
        [Display(Name = "Электрик")]
        Электрик,

        [Display(Name = "Плотник")]
        Плотник,

        [Display(Name = "Сантехник")]
        Сантехник,

        [Display(Name = "Универсал")]
        Универсал
    }
}
