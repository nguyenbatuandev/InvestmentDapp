namespace InvestDapp.Shared.Models.Trading
{
    public class Symbol
    {
        public string Name { get; set; } = string.Empty;
        public string BaseAsset { get; set; } = string.Empty;
        public string QuoteAsset { get; set; } = string.Empty;
        public decimal TickSize { get; set; }
        public decimal StepSize { get; set; }
        public int MaxLeverage { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class SymbolInfo
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal PriceFilter { get; set; }
        public decimal LotSizeFilter { get; set; }
        public int MaxLeverage { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class KlineData
    {
        public string Symbol { get; set; } = string.Empty;
        public string Interval { get; set; } = string.Empty;
        public long OpenTime { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public long CloseTime { get; set; }
        public bool IsKlineClosed { get; set; }
    }

    public class MarkPriceData
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal MarkPrice { get; set; }
        public decimal IndexPrice { get; set; }
        public decimal FundingRate { get; set; }
        public long NextFundingTime { get; set; }
        public long EventTime { get; set; }
    }

    public class FundingRateData
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal FundingRate { get; set; }
        public long FundingTime { get; set; }
        public long NextFundingTime { get; set; }
    }

    public class MarketStats
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal PriceChange { get; set; }
        public decimal PriceChangePercent { get; set; }
        public decimal LastPrice { get; set; }
        public decimal Volume { get; set; }
        public decimal QuoteVolume { get; set; }
        public decimal High24h { get; set; }
        public decimal Low24h { get; set; }
        public decimal OpenPrice { get; set; }
        public long Count { get; set; }
    }
}