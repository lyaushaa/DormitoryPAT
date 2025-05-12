using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormitoryPAT.Models
{
    [PrimaryKey(nameof(MasterId), nameof(SpecialtyId))] // Добавьте этот атрибут
    public class MasterEmployees
    {
        [ForeignKey("Employees")]
        public int MasterId { get; set; }

        [ForeignKey("MasterSpecialties")]
        public int SpecialtyId { get; set; }

        // Навигационные свойства
        public Employees Employee { get; set; }
        public MasterSpecialties MasterSpecialty { get; set; }
    }
}
