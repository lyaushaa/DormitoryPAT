using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public EducatorFloor Floor { get; set; }

        // Навигационное свойство
        public Employees Employees { get; set; }
    }

    public enum EducatorFloor
    {
        [Display(Name = "2-4")]
        Floor2_4,
        [Display(Name = "5-7")]
        Floor5_7
    }
}
