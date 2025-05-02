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
    public class Complaints
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ComplaintId { get; set; }

        [ForeignKey("Users")]
        public int? UserId { get; set; }

        [Required]
        public string ComplaintText { get; set; }

        [ForeignKey("Users")]
        public int? ReviewedBy { get; set; }

        [Required]
        public ComplaintStatus Status { get; set; }

        public string Comment { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime SubmissionDate { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime LastStatusChange { get; set; }

        // Навигационные свойства
        public Users Users { get; set; }
        public Users ReviewedByUser { get; set; }
    }

    public enum ComplaintStatus
    {
        Новый,
        [Display(Name = "На рассмотрении")]
        На_рассмотрении,
        Выполнено,
        Отклонено
    }
}
