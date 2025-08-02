using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Models
{
    public class Campaign
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Cho phép EF tự động tạo ID
        public int Id { get; set; }

        [Required]
        [StringLength(42)]
        public string OwnerAddress { get; set; }

        [Required]
        public string Name { get; set; }

        // --- Dữ liệu Off-Chain (tùy chọn nhưng rất hữu ích) ---
        public string? ShortDescription { get; set; } // Mô tả ngắn gọn
        public string? Description { get; set; } // Mô tả chi tiết
        public string? ImageUrl { get; set; }    // URL ảnh bìa
                                                 // ✅ BƯỚC 1: THÊM KHÓA NGOẠI VÀ CHO PHÉP NULL
        public int? categoryId { get; set; } // Thêm khóa ngoại categoryId, dấu ? cho phép nó null

        public virtual Category? category { get; set; }

        // ✅ THÊM TRẠNG THÁI DUYỆT
        public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
        public string? AdminNotes { get; set; } // Ghi chú của admin khi duyệt/từ chối
        public DateTime? ApprovedAt { get; set; } // Thời gian duyệt
        public string? ApprovedBy { get; set; } // Admin đã duyệt

        // --- Dữ liệu On-Chain ---
        [Required]
        public double GoalAmount { get; set; }

        public double CurrentRaisedAmount { get; set; }

        public double TotalInvestmentsOnCompletion { get; set; } // Ánh xạ từ 'totalinvestments'

        public double TotalProfitAdded { get; set; } // Ánh xạ từ 'totalProfits'

        public DateTime EndTime { get; set; }
        public CampaignStatus Status { get; set; }
        public int InvestorCount { get; set; }
        public int DeniedWithdrawalRequestCount { get; set; } // Ánh xạ từ 'getDenialsRequestedWithDrawCampaigns'

        public DateTime CreatedAt { get; set; } // Ngày tạo chiến dịch

        // --- Mối quan hệ (Navigation Properties) ---
        public virtual ICollection<Investment> Investments { get; set; } = new List<Investment>();
        public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
        public virtual ICollection<Profit> Profits { get; set; } = new List<Profit>();
        public virtual Refund Refund { get; set; }

        // ✅ THÊM RELATIONSHIP VỚI CAMPAIGNPOST
        public virtual ICollection<CampaignPost> Posts { get; set; } = new List<CampaignPost>();
    }
}
