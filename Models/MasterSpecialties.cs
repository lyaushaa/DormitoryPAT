using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DormitoryPAT.Models
{
    public class MasterSpecialties
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SpecialtyId { get; set; }

        [Required]
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
