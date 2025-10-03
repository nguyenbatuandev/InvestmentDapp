using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models.Support;

namespace InvestDapp.Infrastructure.Data.interfaces
{
    public class SupportTicketQuery
    {
        public int? UserId { get; set; }
        public int? AssignedToUserId { get; set; }
        public SupportTicketStatus? Status { get; set; }
        public SupportTicketPriority? Priority { get; set; }
        public SupportTicketSlaStatus? SlaStatus { get; set; }
        public string? Keyword { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 20;
        public bool IncludeMessages { get; set; }
    }

    public record SupportStaffSummary(int UserId, string DisplayName, string? Email);

    public interface ISupportTicketRepository
    {
        Task<SupportTicket> CreateTicketAsync(SupportTicket ticket, SupportTicketMessage firstMessage, IEnumerable<SupportTicketAttachment>? attachments, CancellationToken cancellationToken = default);
        Task<SupportTicket?> GetTicketByIdAsync(int ticketId, bool includeDetails = false, CancellationToken cancellationToken = default);
        Task<(IReadOnlyList<SupportTicket> Items, int Total)> QueryTicketsAsync(SupportTicketQuery query, CancellationToken cancellationToken = default);
        Task AddMessageAsync(SupportTicketMessage message, IEnumerable<SupportTicketAttachment>? attachments, CancellationToken cancellationToken = default);
        Task AssignAsync(int ticketId, int assignedToUserId, int assignedByUserId, string? notes, CancellationToken cancellationToken = default);
        Task UpdateTicketAsync(SupportTicket ticket, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SupportTicket>> GetOpenTicketsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SupportStaffSummary>> GetAssignableStaffAsync(CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
