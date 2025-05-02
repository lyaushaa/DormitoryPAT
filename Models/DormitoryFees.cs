using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DormitoryPAT.Models
{
    public class DormitoryFees
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FeeId { get; set; }

        [Required]
        [MaxLength(255)]
        public string StudentCategory { get; set; }

        [Required]
        [MaxLength(255)]
        public string RentFee { get; set; }

        [Required]
        [MaxLength(255)]
        public string UtilityFee { get; set; }

        [Required]
        [MaxLength(255)]
        public string TotalFee { get; set; }
    }
}
