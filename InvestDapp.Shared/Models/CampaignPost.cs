using InvestDapp.Models;
using InvestDapp.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models
{
    /// <summary>
    /// Model cho bài vi?t/tin t?c v? chi?n d?ch
    /// </summary>
    public class CampaignPost
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CampaignId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public PostType PostType { get; set; } = PostType.Update;

        public string? ImageUrl { get; set; }

        [Required]
        [StringLength(42)]
        public string AuthorAddress { get; set; } // Đ?a ch? ví c?a ngư?i t?o bài vi?t

        public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

        public string? AdminNotes { get; set; } // Ghi chú c?a admin

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ApprovedAt { get; set; }

        public string? ApprovedBy { get; set; } // Admin đ? duy?t

        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        [ForeignKey("CampaignId")]
        public virtual Campaign Campaign { get; set; }

        // Thêm các thông tin b? sung
        public int ViewCount { get; set; } = 0;
        public bool IsFeatured { get; set; } = false; // Bài vi?t n?i b?t
        public string? Tags { get; set; } // Tags phân cách b?ng d?u ph?y
    }
}