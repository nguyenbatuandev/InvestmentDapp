using System.ComponentModel.DataAnnotations;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Common.Respone;
using InvestDapp.Shared.Enums;

namespace InvestDapp.Areas.admin.ViewModels.Support
{
    public class SupportTicketListViewModel
    {
        public SupportTicketFilterRequest Filter { get; set; } = new();
        public IReadOnlyList<SupportTicketSummaryResponse> Tickets { get; set; } = Array.Empty<SupportTicketSummaryResponse>();
        public SupportTicketListMetrics Metrics { get; set; } = new();
        public string Scope { get; set; } = "inbox";
        public int TotalItems { get; set; }

        public int TotalPages => Filter.PageSize <= 0
            ? 1
            : Math.Max(1, (int)Math.Ceiling(TotalItems / (double)Filter.PageSize));

        public bool HasPreviousPage => Filter.Page > 1;
        public bool HasNextPage => Filter.Page < TotalPages;
    }

    public class SupportTicketListMetrics
    {
        public int TotalOpen { get; set; }
        public int Unassigned { get; set; }
        public int WaitingForCustomer { get; set; }
        public int SlaAtRisk { get; set; }
        public int NewToday { get; set; }
    }

    public class SupportTicketDetailViewModel
    {
        public SupportTicketDetailResponse Ticket { get; set; } = new();
        public IReadOnlyList<SupportStaffSummaryResponse> StaffOptions { get; set; } = Array.Empty<SupportStaffSummaryResponse>();
        public SupportTicketReplyForm ReplyForm { get; set; } = new();
        public SupportTicketAssignForm AssignForm { get; set; } = new();
        public bool CanMarkResolved => Ticket.Status != SupportTicketStatus.Resolved && Ticket.Status != SupportTicketStatus.Closed;
        public bool CanClose => Ticket.Status != SupportTicketStatus.Closed;
    }

    public class SupportTicketReplyForm
    {
        [Required]
        public int TicketId { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Message { get; set; } = string.Empty;

        public bool MarkAsResolved { get; set; }
        public bool TransitionToCustomerWaiting { get; set; }
    }

    public class SupportTicketAssignForm
    {
        [Required]
        public int TicketId { get; set; }

        [Required]
        public int AssignedToUserId { get; set; }

        [MaxLength(250)]
        public string? Notes { get; set; }
    }
}
