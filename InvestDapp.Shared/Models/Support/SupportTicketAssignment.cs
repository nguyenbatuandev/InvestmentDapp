using System.ComponentModel.DataAnnotations;

namespace InvestDapp.Shared.Models.Support
{
    public class SupportTicketAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TicketId { get; set; }

        public SupportTicket? Ticket { get; set; }

        [Required]
        public int AssignedToUserId { get; set; }

        public User? AssignedToUser { get; set; }

        [Required]
        public int AssignedByUserId { get; set; }

        public User? AssignedByUser { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(250)]
        public string? Notes { get; set; }
    }
}
