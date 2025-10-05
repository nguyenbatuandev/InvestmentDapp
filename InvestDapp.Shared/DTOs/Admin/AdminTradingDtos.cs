using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models.Trading;

namespace InvestDapp.Shared.DTOs.Admin
{
    /// <summary>
    /// Dashboard Trading Analytics
    /// </summary>
    public class TradingDashboardDto
    {
        public TradingStatsDto Stats { get; set; } = new();
        public List<TopTraderDto> TopTraders { get; set; } = new();
        public List<TradingActivityDto> RecentActivities { get; set; } = new();
        public List<PendingWithdrawalDto> PendingWithdrawals { get; set; } = new();
        public TradingFeeConfig? CurrentFeeConfig { get; set; }
    }

    public class TradingStatsDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public decimal TotalBalance { get; set; }
        public decimal TotalMarginUsed { get; set; }
        public int TotalOrders { get; set; }
        public int OpenPositions { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal TotalFees { get; set; }
        public int PendingWithdrawals { get; set; }
        public decimal PendingWithdrawalAmount { get; set; }
    }

    public class TopTraderDto
    {
        public string UserWallet { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public decimal TotalPnL { get; set; }
        public int TotalOrders { get; set; }
        public int OpenPositions { get; set; }
        public decimal WinRate { get; set; }
        public DateTime LastActive { get; set; }
    }

    public class TradingActivityDto
    {
        public string Type { get; set; } = string.Empty; // ORDER, DEPOSIT, WITHDRAWAL
        public string UserWallet { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public decimal Amount { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PendingWithdrawalDto
    {
        public int Id { get; set; }
        public string UserWallet { get; set; } = string.Empty;
        public string RecipientAddress { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Fee { get; set; }
        public decimal NetAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public int PendingDays { get; set; }
    }

    /// <summary>
    /// Chi tiết User Trading
    /// </summary>
    public class UserTradingDetailDto
    {
        public string UserWallet { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal Balance { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal MarginUsed { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal TotalFees { get; set; }
        public int TotalOrders { get; set; }
        public int ClosedOrders { get; set; }
        public int OpenPositions { get; set; }
        public decimal WinRate { get; set; }
        public int WinningOrders { get; set; }
        public int TotalWithdrawals { get; set; }
        public DateTime? FirstOrderDate { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public DateTime LastUpdated { get; set; }

        public List<OrderDetailDto> Orders { get; set; } = new();
        public List<PositionDetailDto> Positions { get; set; } = new();
        public List<BalanceTransactionDto> Transactions { get; set; } = new();
        public List<WithdrawalDetailDto> Withdrawals { get; set; } = new();
        public List<TradingAccountLockDto> ActiveLocks { get; set; } = new();
    }

    public class OrderDetailDto
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty; // BUY, SELL
        public string Type { get; set; } = string.Empty; // LONG, SHORT
        public decimal Size { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal? ExitPrice { get; set; }
        public decimal PnL { get; set; }
        public decimal Fee { get; set; }
        public string Status { get; set; } = string.Empty; // OPEN, CLOSED
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
    }

    public class PositionDetailDto
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // LONG, SHORT
        public decimal Size { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal Margin { get; set; }
        public decimal Leverage { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class WithdrawalDetailDto
    {
        public int Id { get; set; }
        public string RecipientAddress { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Fee { get; set; }
        public decimal NetAmount { get; set; }
        public string Status { get; set; } = string.Empty; // PENDING, APPROVED, REJECTED
        public string? ProcessedByAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    public class OrderDto
    {
        public int Id { get; set; }
        public string? InternalOrderId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal? Price { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal FilledQuantity { get; set; }
        public decimal AvgPrice { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PositionDto
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Size { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal MarkPrice { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public decimal RealizedPnl { get; set; }
        public int Leverage { get; set; }
        public decimal Margin { get; set; }
        public decimal? LiquidationPrice { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BalanceTransactionDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Description { get; set; }
        public decimal BalanceAfter { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class WithdrawalRequestDto
    {
        public int Id { get; set; }
        public string RecipientAddress { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? AdminNotes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TradingAccountLockDto
    {
        public int Id { get; set; }
        public string LockType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string LockedByAdmin { get; set; } = string.Empty;
        public DateTime LockedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Request DTOs
    /// </summary>
    public class ApproveWithdrawalRequest
    {
        public int WithdrawalId { get; set; }
        public string? AdminNotes { get; set; }
    }

    public class RejectWithdrawalRequest
    {
        public int WithdrawalId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class LockAccountRequest
    {
        public string UserWallet { get; set; } = string.Empty;
        public AccountLockType LockType { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }

    public class UnlockAccountRequest
    {
        public int LockId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class AdjustBalanceRequest
    {
        public string UserWallet { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class UpdateFeeConfigRequest
    {
        public double MakerFeePercent { get; set; }
        public double TakerFeePercent { get; set; }
        public double WithdrawalFeePercent { get; set; }
        public double MinWithdrawalFee { get; set; }
        public double MinWithdrawalAmount { get; set; }
        public double MaxWithdrawalAmount { get; set; }
        public double DailyWithdrawalLimit { get; set; }
        public string? Notes { get; set; }
    }
}
