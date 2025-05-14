using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

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

        [ForeignKey("Reviewer")]
        public int? ReviewedBy { get; set; }

        [Required]
        [Column(TypeName = "ENUM('Новая', 'На_рассмотрении', 'Выполнена', 'Отклонена')")]
        public ComplaintStatus Status { get; set; }

        public string? Comment { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime SubmissionDate { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime LastStatusChange { get; set; }

        // Навигационные свойства
        public Users Users { get; set; }
        public Employees Reviewer { get; set; }
    }

    public enum ComplaintStatus
    {
        [Display(Name = "Новая")]
        Новая,

        [Display(Name = "На_рассмотрении")]
        На_рассмотрении,

        [Display(Name = "Выполнена")]
        Выполнена,

        [Display(Name = "Отклонена")]
        Отклонена
    }
}
