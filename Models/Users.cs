using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DormitoryPAT.Models
{
    public class Users
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string FullName { get; set; }

        [Required]
        public long PhoneNumber { get; set; }

        public long? TelegramId { get; set; }

        [Required]
        [Column(TypeName = "ENUM('Студент', 'Сотрудник')")]
        public UserRole Role { get; set; }

        // Навигационные свойства
        public Students Students { get; set; }
        public Employees Employees { get; set; }
        public ICollection<RepairRequests> RepairRequests { get; set; }
        public ICollection<DutySchedule> DutySchedules { get; set; }
        [InverseProperty("Users")]
        public ICollection<Complaints> SubmittedComplaints { get; set; }
        [InverseProperty("Reviewer")]
        public ICollection<Complaints> ReviewedComplaints { get; set; }
    }

    public enum UserRole
    {
        [Display(Name = "Студент")]
        Студент,

        [Display(Name = "Сотрудник")]
        Сотрудник
    }
}
