using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models.BlockchainModels
{
    public class EventLogBlockchain
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // ✅ Chỉ rõ auto-increment
        public int Id { get; set; }

        public string EventType { get; set; } = string.Empty;
        public string TransactionHash { get; set; } = string.Empty;
        public int BlockNumber { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string EventData { get; set; } = string.Empty; // Lưu dữ liệu gốc dạng JSON
        public int CampaignId { get; set; }
    }
}
