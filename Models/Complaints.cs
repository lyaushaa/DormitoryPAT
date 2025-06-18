using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DormitoryPAT.Models
{
    public class Complaints
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ComplaintId { get; set; }
        [ForeignKey("Student")]
        public long StudentId { get; set; }
        [Required]
        public string ComplaintText { get; set; }
        [ForeignKey("Reviewer")]
        public long? ReviewedBy { get; set; }
        [Required]
        [Column(TypeName = "ENUM('Создана', 'В_обработке', 'Завершена', 'Отклонена')")]
        public ComplaintStatus Status { get; set; }
        public string? Comment { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime SubmissionDate { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime LastStatusChange { get; set; }
        // Навигационные свойства
        public Students Student { get; set; }
        
        
        [NotMapped]
        public string StatusDisplay => Status switch
        {
            ComplaintStatus.Создана => "Создана",
            ComplaintStatus.В_обработке => "В обработке",
            ComplaintStatus.Завершена => "Завершена",
            ComplaintStatus.Отклонена => "Отклонена",
            _ => Status.ToString()
        };
    }

    public enum ComplaintStatus
    {
        [Display(Name = "Создана")]
        Создана,
        [Display(Name = "В_обработке")]
        В_обработке,
        [Display(Name = "Завершена")]
        Завершена,
        [Display(Name = "Отклонена")]
        Отклонена
    }
}