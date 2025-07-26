using InvestDapp.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models
{
    public class Investment
    {
        [Key]
        public int Id { get; set; } // ID tự tăng

        [Required]
        [StringLength(66)]
        public string TransactionHash { get; set; } // Dùng để chống trùng lặp

        // --- Foreign Key đến Campaign ---
        public int CampaignId { get; set; }
        public virtual Campaign Campaign { get; set; }

        // --- Chi tiết khoản đầu tư ---
        [Required]
        [StringLength(42)]
        public string InvestorAddress { get; set; }

        [Required]
        public double Amount { get; set; }

        public DateTime Timestamp { get; set; }

    }
}
