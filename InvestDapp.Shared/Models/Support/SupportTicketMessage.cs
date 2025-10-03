using System.ComponentModel.DataAnnotations;

namespace InvestDapp.Shared.Models.Support
{
    public class SupportTicketMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TicketId { get; set; }

        public SupportTicket? Ticket { get; set; }

        [Required]
        public int SenderUserId { get; set; }

        public User? SenderUser { get; set; }

        public bool IsFromStaff { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<SupportTicketAttachment> Attachments { get; set; } = new List<SupportTicketAttachment>();
    }
}
