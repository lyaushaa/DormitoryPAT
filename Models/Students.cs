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
        public StudentRole StudentRole { get; set; } = StudentRole.Студент;

        // Навигационное свойство
        public Users Users { get; set; }
    }

    public enum StudentRole
    {
        Студент,
        [Display(Name = "Староста этажа")]
        Староста_этажа,
        [Display(Name = "Председатель общежития")]
        Председатель_общежития
    }
}
