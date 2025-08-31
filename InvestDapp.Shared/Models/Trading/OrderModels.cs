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
        public string UserWallet { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public OrderType Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal? Price { get; set; }
        public decimal? StopPrice { get; set; }
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