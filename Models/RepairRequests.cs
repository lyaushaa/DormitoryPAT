using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DormitoryPAT.Models
{
    public class RepairRequests
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long RequestId { get; set; }
        [Required]
        [ForeignKey("Students")]
        public long StudentId { get; set; }
        [Required]
        [MaxLength(255)]
        public string Location { get; set; }
        [Required]
        [Column(TypeName = "ENUM('Электрика', 'Сантехника', 'Мебель')")]
        public ProblemType Problem { get; set; }
        public string? UserComment { get; set; }
        [ForeignKey("Employees")]
        public long? MasterId { get; set; }
        [Required]
        [Column(TypeName = "ENUM('Создана', 'В_обработке', 'Ожидает_запчастей', 'Завершена', 'Отклонена')")]
        public RequestStatus Status { get; set; }
        public string? MasterComment { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime RequestDate { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime LastStatusChange { get; set; }
        // Навигационные свойства
        public Students Student { get; set; }
        public Employees Master { get; set; }
        [NotMapped]
        public string StatusDisplay => Status switch
        {
            RequestStatus.Создана => "Создана",
            RequestStatus.В_обработке => "В обработке",
            RequestStatus.Ожидает_запчастей => "Ожидает запчастей",
            RequestStatus.Завершена => "Завершена",
            RequestStatus.Отклонена => "Отклонена",
            _ => Status.ToString()
        };
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
        [Display(Name = "Создана")]
        Создана,
        [Display(Name = "В_обработке")]
        В_обработке,
        [Display(Name = "Ожидает_запчастей")]
        Ожидает_запчастей,
        [Display(Name = "Завершена")]
        Завершена,
        [Display(Name = "Отклонена")]
        Отклонена
    }
}