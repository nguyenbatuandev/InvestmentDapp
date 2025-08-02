using InvestDapp.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace InvestDapp.Shared.Common.Request
{
    /// <summary>
    /// Request �? t?o chi?n d?ch m?i
    /// </summary>
    public class CreateCampaignRequest
    {
        [Required(ErrorMessage = "T�n chi?n d?ch l� b?t bu?c")]
        [StringLength(200, ErrorMessage = "T�n chi?n d?ch kh�ng ��?c v�?t qu� 200 k? t?")]
        public string Name { get; set; }

        [Required(ErrorMessage = "M� t? ng?n l� b?t bu?c")]
        [StringLength(500, ErrorMessage = "M� t? ng?n kh�ng ��?c v�?t qu� 500 k? t?")]
        public string ShortDescription { get; set; }

        [Required(ErrorMessage = "M� t? chi ti?t l� b?t bu?c")]
        [StringLength(5000, ErrorMessage = "M� t? chi ti?t kh�ng ��?c v�?t qu� 5000 k? t?")]
        public string Description { get; set; }

        [Required(ErrorMessage = "M?c ti�u s? ti?n l� b?t bu?c")]
        [Range(0.1, double.MaxValue, ErrorMessage = "M?c ti�u ph?i l?n h�n 0")]
        public double GoalAmount { get; set; }

        [Required(ErrorMessage = "Th?i gian k?t th�c l� b?t bu?c")]
        public DateTime EndTime { get; set; }

        [Url(ErrorMessage = "URL h?nh ?nh kh�ng h?p l?")]
        public string? ImageUrl { get; set; }

        public int? CategoryId { get; set; }
    }

    /// <summary>
    /// Request �? t?o b�i vi?t chi?n d?ch
    /// </summary>
    public class CreateCampaignPostRequest
    {
        [Required(ErrorMessage = "ID chi?n d?ch l� b?t bu?c")]
        public int CampaignId { get; set; }

        [Required(ErrorMessage = "Ti�u �? l� b?t bu?c")]
        [StringLength(200, ErrorMessage = "Ti�u �? kh�ng ��?c v�?t qu� 200 k? t?")]
        public string Title { get; set; }

        [Required(ErrorMessage = "N?i dung l� b?t bu?c")]
        public string Content { get; set; }

        public PostType PostType { get; set; } = PostType.Update;

        [Url(ErrorMessage = "URL h?nh ?nh kh�ng h?p l?")]
        public string? ImageUrl { get; set; }

        public string? Tags { get; set; }

        public bool IsFeatured { get; set; } = false;
    }

    /// <summary>
    /// Request �? admin duy?t
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