using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DormitoryPAT.Models
{
    public class MasterEmployees
    {
        [ForeignKey("Employees")]
        public int MasterId { get; set; }

        [ForeignKey("MasterSpecialties")]
        public int SpecialtyId { get; set; }

        // Навигационные свойства
        public Employees Employees { get; set; }
        public MasterSpecialties MasterSpecialties { get; set; }
    }
}
