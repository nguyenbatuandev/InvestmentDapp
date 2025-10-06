using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models
{
    public class ProfitClaim
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ProfitId { get; set; }
    public virtual Profit Profit { get; set; } = null!;

        [Required]
        [MaxLength(42)]
        public string ClaimerWallet { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? TransactionHash { get; set; }

        public DateTime ClaimedAt { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Amount { get; set; }
    }
}
