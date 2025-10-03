using System.ComponentModel.DataAnnotations;
using InvestDapp.Shared.Enums;

namespace InvestDapp.Shared.Common.Request
{
    public class CreateSupportTicketRequest
    {
        [Required]
        [MaxLength(150)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Category { get; set; }

        public SupportTicketPriority Priority { get; set; } = SupportTicketPriority.Normal;

        [Required]
        [MaxLength(4000)]
        public string Message { get; set; } = string.Empty;
    }
}
