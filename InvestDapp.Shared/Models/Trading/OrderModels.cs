namespace InvestDapp.Shared.Models.Trading
{
    public enum OrderSide
    {
        Buy,
        Sell
    }

    public enum OrderType
    {
        Market,
        Limit,
        StopMarket,
        StopLimit
    }

    public enum OrderStatus
    {
        Pending,
        Filled,
        PartiallyFilled,
        Cancelled,
        Rejected
    }

    // Database models for EF Core
    public class Order
    {
        public int Id { get; set; }
    // Link to in-memory internal order id for correlation
    public string? InternalOrderId { get; set; }
        public string UserWallet { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public OrderType Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal? Price { get; set; }
        public decimal? StopPrice { get; set; }
    // New advanced trading fields
    public decimal? TakeProfitPrice { get; set; }
    public decimal? StopLossPrice { get; set; }
    public bool ReduceOnly { get; set; } = false;
        public int Leverage { get; set; } = 1;
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public decimal FilledQuantity { get; set; } = 0;
        public decimal AvgPrice { get; set; } = 0;
        public string? Notes { get; set; }
    }

    public class Position
    {
        public int Id { get; set; }
        public string UserWallet { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public decimal Size { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal MarkPrice { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public decimal RealizedPnl { get; set; }
        public int Leverage { get; set; }
        public decimal Margin { get; set; }
        public decimal PnL { get; set; }
    // New risk / TP-SL fields
    public decimal? TakeProfitPrice { get; set; }
    public decimal? StopLossPrice { get; set; }
    public decimal MaintenanceMarginRate { get; set; } = 0.005m; // 0.5% default
    public bool IsIsolated { get; set; } = true;
    public decimal? LiquidationPrice { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class UserBalance
    {
        public int Id { get; set; }
        public string UserWallet { get; set; } = string.Empty;
        public decimal Balance { get; set; } = 0;
        public decimal AvailableBalance { get; set; } = 0;
        public decimal MarginUsed { get; set; } = 0;
        public decimal UnrealizedPnl { get; set; } = 0;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // Deposit / Withdraw / Trading balance history
    public class BalanceTransaction
    {
        public int Id { get; set; }
        public string UserWallet { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty; // DEPOSIT, WITHDRAW
        public string? Reference { get; set; } // optional ref (order id etc.)
        public string? Description { get; set; }
        public decimal BalanceAfter { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // In-memory models for internal trading engine
    public class InternalOrder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public OrderType Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal? Price { get; set; }
        public decimal? StopPrice { get; set; }
    public decimal? TakeProfitPrice { get; set; }
    public decimal? StopLossPrice { get; set; }
    // Optional: when set, reduce-only execution should target this specific DB position id first
    public string? TargetPositionId { get; set; }
    public bool ReduceOnly { get; set; } = false;
        public int Leverage { get; set; } = 1;
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public decimal FilledQuantity { get; set; } = 0;
        public decimal AveragePrice { get; set; } = 0;
        public string? Notes { get; set; }
    }

    public class InternalPosition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public decimal Size { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal MarkPrice { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public decimal RealizedPnl { get; set; }
        public int Leverage { get; set; }
        public decimal Margin { get; set; }
    public decimal PnL { get; set; } // convenience aggregate (Realized + Unrealized)
    public decimal? TakeProfitPrice { get; set; }
    public decimal? StopLossPrice { get; set; }
    public decimal MaintenanceMarginRate { get; set; } = 0.005m;
    public bool IsIsolated { get; set; } = true;
    public decimal? LiquidationPrice { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class InternalUserBalance
    {
        public string UserId { get; set; } = string.Empty;
        public decimal Balance { get; set; } = 0;
        public decimal AvailableBalance { get; set; } = 0;
        public decimal UsedMargin { get; set; } = 0;
        public decimal UnrealizedPnl { get; set; } = 0;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}