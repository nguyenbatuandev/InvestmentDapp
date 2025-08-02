using InvestDapp.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace InvestDapp.Shared.Common.Request
{
    /// <summary>
    /// Request ð? t?o chi?n d?ch m?i
    /// </summary>
    public class CreateCampaignRequest
    {
        [Required(ErrorMessage = "Tên chi?n d?ch là b?t bu?c")]
        [StringLength(200, ErrorMessage = "Tên chi?n d?ch không ðý?c vý?t quá 200 k? t?")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Mô t? ng?n là b?t bu?c")]
        [StringLength(500, ErrorMessage = "Mô t? ng?n không ðý?c vý?t quá 500 k? t?")]
        public string ShortDescription { get; set; }

        [Required(ErrorMessage = "Mô t? chi ti?t là b?t bu?c")]
        [StringLength(5000, ErrorMessage = "Mô t? chi ti?t không ðý?c vý?t quá 5000 k? t?")]
        public string Description { get; set; }

        [Required(ErrorMessage = "M?c tiêu s? ti?n là b?t bu?c")]
        [Range(0.1, double.MaxValue, ErrorMessage = "M?c tiêu ph?i l?n hõn 0")]
        public double GoalAmount { get; set; }

        [Required(ErrorMessage = "Th?i gian k?t thúc là b?t bu?c")]
        public DateTime EndTime { get; set; }

        [Url(ErrorMessage = "URL h?nh ?nh không h?p l?")]
        public string? ImageUrl { get; set; }

        public int? CategoryId { get; set; }
    }

    /// <summary>
    /// Request ð? t?o bài vi?t chi?n d?ch
    /// </summary>
    public class CreateCampaignPostRequest
    {
        [Required(ErrorMessage = "ID chi?n d?ch là b?t bu?c")]
        public int CampaignId { get; set; }

        [Required(ErrorMessage = "Tiêu ð? là b?t bu?c")]
        [StringLength(200, ErrorMessage = "Tiêu ð? không ðý?c vý?t quá 200 k? t?")]
        public string Title { get; set; }

        [Required(ErrorMessage = "N?i dung là b?t bu?c")]
        public string Content { get; set; }

        public PostType PostType { get; set; } = PostType.Update;

        [Url(ErrorMessage = "URL h?nh ?nh không h?p l?")]
        public string? ImageUrl { get; set; }

        public string? Tags { get; set; }

        public bool IsFeatured { get; set; } = false;
    }

    /// <summary>
    /// Request ð? admin duy?t
    /// </summary>
    public class ApprovalRequest
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public ApprovalStatus Status { get; set; }

        public string? AdminNotes { get; set; }
    }
}