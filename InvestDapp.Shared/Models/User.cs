using InvestDapp.Models;
using InvestDapp.Shared.Models.Kyc;
using InvestDapp.Shared.Models.Message;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models
{
    public class User
    {
        [Key]
        public int ID { get; set; }

        [Required]
        [MaxLength(42)]
        public string WalletAddress { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Email { get; set; }

        [MaxLength(255)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Avatar { get; set; }

        public string? Bio { get; set; }


        [MaxLength(50)]
        public string Role { get; set; } = "User";


        [MaxLength(255)]
        public string? Nonce { get; set; }
        public DateTime? NonceGeneratedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? DeletedAt { get; set; }  // Soft delete (manually handled)

        // Relationships
        //public ICollection<Campaign> Campaigns { get; set; }
        //public ICollection<Investment> Investment { get; set; }
        //public ICollection<Vote> Votes { get; set; }
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<FundraiserKyc> FundraiserKyc { get; set; } = new List<FundraiserKyc>();
        // --- Navigation Properties ---
        [InverseProperty("Sender")]
        public virtual ICollection<Messager> SentMessages { get; set; } = new List<Messager>();
        public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();
    }
}
