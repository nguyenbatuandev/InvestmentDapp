using System.ComponentModel.DataAnnotations;

namespace InvestDapp.Shared.Common.Request
{
    public class AddSupportTicketMessageRequest
    {
        [Required]
        public int TicketId { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Message { get; set; } = string.Empty;

        public bool TransitionToCustomerWaiting { get; set; }
    }
}
