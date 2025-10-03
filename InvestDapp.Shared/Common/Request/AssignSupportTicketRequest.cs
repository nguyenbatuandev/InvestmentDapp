using System.ComponentModel.DataAnnotations;

namespace InvestDapp.Shared.Common.Request
{
    public class AssignSupportTicketRequest
    {
        [Required]
        public int TicketId { get; set; }

        [Required]
        public int AssignedToUserId { get; set; }

        [MaxLength(250)]
        public string? Notes { get; set; }
    }
}
