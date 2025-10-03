using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InvestDapp.Shared.Enums;

namespace InvestDapp.Shared.Models.Support
{
    public class SupportTicket
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string TicketCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Category { get; set; }

        [Required]
        public SupportTicketPriority Priority { get; set; } = SupportTicketPriority.Normal;

        [Required]
        public SupportTicketStatus Status { get; set; } = SupportTicketStatus.New;

        [Required]
        public SupportTicketSlaStatus SlaStatus { get; set; } = SupportTicketSlaStatus.OnTrack;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? DueAt { get; set; }

        public DateTime? FirstResponseAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public DateTime? ClosedAt { get; set; }

        public DateTime? LastCustomerReplyAt { get; set; }

        public DateTime? LastAgentReplyAt { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        public User? User { get; set; }

        public int? AssignedToUserId { get; set; }

        public User? AssignedToUser { get; set; }

        public DateTime? AssignedAt { get; set; }

        [MaxLength(250)]
        public string? AutomationNotes { get; set; }

        public ICollection<SupportTicketMessage> Messages { get; set; } = new List<SupportTicketMessage>();

        public ICollection<SupportTicketAssignment> Assignments { get; set; } = new List<SupportTicketAssignment>();
    }
}
