namespace InvestDapp.Infrastructure.Data.Config
{
    public class BinanceConfig
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string WebSocketUrl { get; set; } = string.Empty;
        public List<string> SupportedSymbols { get; set; } = new();
        public List<string> SupportedIntervals { get; set; } = new();
        public int MaxKlinesHistory { get; set; } = 1000;
        public int WebSocketReconnectDelayMs { get; set; } = 5000;
        public int RestApiRateLimitPerMinute { get; set; } = 1200;
    }

    public class TradingConfig
    {
        public Dictionary<string, int> MaxLeveragePerSymbol { get; set; } = new();
        public decimal DefaultBalance { get; set; } = 10000;
    public decimal MinOrderSize { get; set; } = 0.0001m; // allow smaller granularity
    public decimal MaxOrderSize { get; set; } = 100; // tighter cap to mitigate accidental huge notional
    // Max raw notional (price * qty) allowed per order AFTER price sanitization
    public decimal MaxNotionalPerOrder { get; set; } = 50_000_000m; // configurable instead of hard-coded in service
    }

    public class RedisConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int Database { get; set; } = 0;
        public string KeyPrefix { get; set; } = "InvestDapp:";
        public int DefaultExpiration { get; set; } = 3600;
    }
}