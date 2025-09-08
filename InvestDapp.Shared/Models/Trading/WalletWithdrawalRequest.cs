using InvestDapp.Shared.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models.Trading
{
    public class WalletWithdrawalRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(42)]
        public string UserWallet { get; set; }

        [Required]
        [MaxLength(100)]
        public string RecipientAddress { get; set; }

        [Required]
        public decimal Amount { get; set; }

        public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;

        public string? AdminNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
