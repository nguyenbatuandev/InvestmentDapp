using InvestDapp.Shared.Models.Trading;

namespace InvestDapp.Shared.Common.Request
{
    public class CreateOrderRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public OrderType Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal? Price { get; set; }
        public decimal? StopPrice { get; set; }
        public int Leverage { get; set; } = 1;
        public decimal? TakeProfitPrice { get; set; }
        public decimal? StopLossPrice { get; set; }
        public bool ReduceOnly { get; set; } = false;
    }

    public class UpdatePositionRiskRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal? TakeProfitPrice { get; set; }
        public decimal? StopLossPrice { get; set; }
        public string? PositionId { get; set; }
    }

    public class BalanceChangeRequest
    {
        public decimal Amount { get; set; }
        public string? RecipientAddress { get; set; }
    }

    public class ClosePositionRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string? PositionId { get; set; }
    }
}
