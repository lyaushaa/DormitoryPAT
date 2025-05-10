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
    public class RepairRequests
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RequestId { get; set; }

        [Required]
        [ForeignKey("Users")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Location { get; set; }

        [Required]
        public ProblemType Problem { get; set; }

        public string UserComment { get; set; }

        [ForeignKey("Employees")]
        public int? MasterId { get; set; }

        [Required]
        public RequestStatus Status { get; set; }

        public string MasterComment { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime RequestDate { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime LastStatusChange { get; set; }

        // Навигационные свойства
        public Users Users { get; set; }
        public Employees Master { get; set; }
    }

    public enum ProblemType
    {
        [Display(Name = "Электрика")]
        Электрика,

        [Display(Name = "Сантехника")]
        Сантехника,

        [Display(Name = "Мебель")]
        Мебель
    }

    public enum RequestStatus
    {
        [Display(Name = "Новая")]
        Новая,

        [Display(Name = "В процессе")]
        В_процессе,

        [Display(Name = "Завершена")]
        Завершена,

        [Display(Name = "Отклонена")]
        Отклонена
    }
}
