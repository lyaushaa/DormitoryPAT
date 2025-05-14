using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

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
        [Column(TypeName = "ENUM('Электрика', 'Сантехника', 'Мебель')")]
        public ProblemType Problem { get; set; }

        public string UserComment { get; set; }

        [ForeignKey("Employees")]
        public int? MasterId { get; set; }

        [Required]
        [Column(TypeName = "ENUM('Новая', 'В_процессе', 'Завершена', 'Отклонена')")]
        public RequestStatus Status { get; set; }

        public string? MasterComment { get; set; }

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

        [Display(Name = "В_процессе")]
        В_процессе,

        [Display(Name = "Завершена")]
        Завершена,

        [Display(Name = "Отклонена")]
        Отклонена
    }
}
