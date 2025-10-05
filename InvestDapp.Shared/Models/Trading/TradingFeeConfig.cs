using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models.Trading
{
    /// <summary>
    /// Cấu hình phí giao dịch Trading
    /// </summary>
    public class TradingFeeConfig
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Tên cấu hình
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Phí Maker (%) - khi đặt lệnh Limit
        /// </summary>
        public double MakerFeePercent { get; set; } = 0.02; // 0.02%

        /// <summary>
        /// Phí Taker (%) - khi đặt lệnh Market
        /// </summary>
        public double TakerFeePercent { get; set; } = 0.04; // 0.04%

        /// <summary>
        /// Phí rút tiền (%) - áp dụng khi rút về ví
        /// </summary>
        public double WithdrawalFeePercent { get; set; } = 0.5; // 0.5%

        /// <summary>
        /// Phí rút tiền tối thiểu (BNB)
        /// </summary>
        public double MinWithdrawalFee { get; set; } = 0.001; // 0.001 BNB

        /// <summary>
        /// Số tiền rút tối thiểu (BNB)
        /// </summary>
        public double MinWithdrawalAmount { get; set; } = 0.01; // 0.01 BNB

        /// <summary>
        /// Số tiền rút tối đa mỗi lần (BNB)
        /// </summary>
        public double MaxWithdrawalAmount { get; set; } = 1000; // 1000 BNB

        /// <summary>
        /// Limit rút tiền mỗi ngày (BNB)
        /// </summary>
        public double DailyWithdrawalLimit { get; set; } = 100; // 100 BNB/day

        /// <summary>
        /// Có đang active không
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}