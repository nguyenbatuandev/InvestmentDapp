using InvestDapp.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models
{
    public class Profit
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // ID được cung cấp bởi Smart Contract
        public int Id { get; set; }
        public double Amount { get; set; } // Số tiền lợi nhuận
        public DateTime CreatedAt { get; set; } // Ngày tạo lợi nhuận
        public string TransactionHash { get; set; } // Hash giao dịch liên quan đến lợi nhuận
        // Navigation property

        // Đây là FK
        public int CampaignId { get; set; }
        public virtual Campaign Campaign { get; set; }

    }
}
