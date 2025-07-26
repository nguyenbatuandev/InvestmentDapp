using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models.BlockchainModels
{
    public class EventProcessingState
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // ✅ Chỉ rõ auto-increment
        public int Id { get; set; }
        public string ContractAddress { get; set; } = string.Empty;
        public long LastProcessedBlock { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
