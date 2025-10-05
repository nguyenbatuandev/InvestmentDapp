using InvestDapp.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models.Trading
{
    /// <summary>
    /// Khóa tài khoản Trading
    /// </summary>
    public class TradingAccountLock
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Ví user bị khóa
        /// </summary>
        [Required]
        [MaxLength(42)]
        public string UserWallet { get; set; } = string.Empty;

        /// <summary>
        /// Loại khóa
        /// </summary>
        public AccountLockType LockType { get; set; } = AccountLockType.Full;

        /// <summary>
        /// Lý do khóa
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Admin thực hiện khóa
        /// </summary>
        [Required]
        [MaxLength(42)]
        public string LockedByAdmin { get; set; } = string.Empty;

        /// <summary>
        /// Thời gian khóa
        /// </summary>
        public DateTime LockedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Thời gian hết hạn khóa (null = vĩnh viễn)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Đã được mở khóa chưa
        /// </summary>
        public bool IsUnlocked { get; set; } = false;

        /// <summary>
        /// Admin mở khóa
        /// </summary>
        [MaxLength(42)]
        public string? UnlockedByAdmin { get; set; }

        /// <summary>
        /// Thời gian mở khóa
        /// </summary>
        public DateTime? UnlockedAt { get; set; }

        [MaxLength(500)]
        public string? UnlockReason { get; set; }
    }
}
