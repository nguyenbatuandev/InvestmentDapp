using InvestDapp.Shared.Enums;

namespace InvestDapp.Shared.Common.Request
{
    public class SupportTicketFilterRequest
    {
        public SupportTicketStatus? Status { get; set; }
        public SupportTicketPriority? Priority { get; set; }
        public SupportTicketSlaStatus? SlaStatus { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Keyword { get; set; }
        public int? AssignedToUserId { get; set; }
    }
}
