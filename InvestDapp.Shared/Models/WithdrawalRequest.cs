using InvestDapp.Models;
using InvestDapp.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models
{
    public class WithdrawalRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)] // ID được cung cấp bởi Smart Contract
        public int Id { get; set; } // ID tự tăng

        // --- Foreign Key đến Campaign ---
        public int CampaignId { get; set; }
        public virtual Campaign Campaign { get; set; }

        // --- Chi tiết yêu cầu ---
        public string RequesterAddress { get; set; } // ID của yêu cầu trong mảng của contract (0, 1, 2...)

        public string txhash { get; set; }

        [Required]
        public string Reason { get; set; }

        [Required]
        public double Amount { get; set; }

        public WithdrawalStatus Status { get; set; }

        [Column(TypeName = "decimal(38,0)")]
        public decimal AgreeVotes { get; set; }

        [Column(TypeName = "decimal(38,0)")]
        public decimal DisagreeVotes { get; set; }

        public DateTime VoteEndTime { get; set; }

        public DateTime CreatedAt { get; set; } // Ngày tạo yêu cầu
        // --- Mối quan hệ ---
        public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
    }
}
