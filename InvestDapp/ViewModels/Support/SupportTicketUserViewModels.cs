using System.ComponentModel.DataAnnotations;
using InvestDapp.Shared.Common.Respone;
using InvestDapp.Shared.Enums;

namespace InvestDapp.ViewModels.Support
{
    public class UserSupportTicketListViewModel
    {
        public SupportTicketListResult Tickets { get; set; } = new();
        public SupportTicketStatus? Status { get; set; }
        public string? Keyword { get; set; }

        public int Page => Tickets.Page;
        public int PageSize => Tickets.PageSize;
        public int TotalItems => Tickets.Total;

        public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
        public bool HasTickets => Tickets.Items.Count > 0;
    }

    public class UserSupportTicketDetailViewModel
    {
        public SupportTicketDetailResponse Ticket { get; set; } = new();
        public UserSupportTicketReplyForm ReplyForm { get; set; } = new();
        public IReadOnlyList<string> AllowedExtensions { get; set; } = Array.Empty<string>();
        public int MaxAttachmentSizeMb { get; set; }
        public bool CanReply => Ticket.Status != SupportTicketStatus.Closed;
    }

    public class UserSupportTicketReplyForm
    {
        [Required]
        public int TicketId { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Message { get; set; } = string.Empty;
    }
}
