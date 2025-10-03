using InvestDapp.Shared.Enums;

namespace InvestDapp.Shared.Common.Respone
{
    public class SupportTicketSummaryResponse
    {
        public int Id { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Category { get; set; }
        public SupportTicketPriority Priority { get; set; }
        public SupportTicketStatus Status { get; set; }
        public SupportTicketSlaStatus SlaStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueAt { get; set; }
        public int? AssignedToUserId { get; set; }
        public string? AssignedToName { get; set; }
        public string RequesterName { get; set; } = string.Empty;
    }

    public class SupportTicketMessageResponse
    {
        public int Id { get; set; }
        public int SenderUserId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public bool IsFromStaff { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<SupportTicketAttachmentResponse> Attachments { get; set; } = new();
    }

    public class SupportTicketAttachmentResponse
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long FileSize { get; set; }
    }

    public class SupportTicketAssignmentHistoryResponse
    {
        public int Id { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public string AssignedByName { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
        public string? Notes { get; set; }
    }

    public class SupportTicketDetailResponse
    {
        public int Id { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string RequesterName { get; set; } = string.Empty;
        public string RequesterWallet { get; set; } = string.Empty;
        public SupportTicketPriority Priority { get; set; }
        public SupportTicketStatus Status { get; set; }
        public SupportTicketSlaStatus SlaStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueAt { get; set; }
        public DateTime? FirstResponseAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int? AssignedToUserId { get; set; }
        public string? AssignedToName { get; set; }
        public List<SupportTicketMessageResponse> Messages { get; set; } = new();
        public List<SupportTicketAssignmentHistoryResponse> Assignments { get; set; } = new();
    }

    public class SupportStaffSummaryResponse
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class SupportTicketListResult
    {
        public IReadOnlyList<SupportTicketSummaryResponse> Items { get; set; } = Array.Empty<SupportTicketSummaryResponse>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
