using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DormitoryPAT.Models
{
    public class DutySchedule
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ScheduleId { get; set; }

        [Required]
        [ForeignKey("Users")]
        public int UserId { get; set; }

        [Required]
        public int Floor { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public int Room { get; set; }

        // Навигационное свойство
        public Users Users { get; set; }
    }
}
