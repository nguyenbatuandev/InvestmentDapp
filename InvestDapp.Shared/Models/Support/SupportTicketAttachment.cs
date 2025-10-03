using System.ComponentModel.DataAnnotations;

namespace InvestDapp.Shared.Models.Support
{
    public class SupportTicketAttachment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MessageId { get; set; }

        public SupportTicketMessage? Message { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public long FileSize { get; set; }
    }
}
