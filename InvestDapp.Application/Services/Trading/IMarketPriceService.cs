using InvestDapp.Infrastructure.Services.Binance;
using Microsoft.Extensions.Logging;

namespace InvestDapp.Application.Services.Trading
{
    public interface IMarketPriceService
    {
        Task<decimal> GetMarkPriceAsync(string symbol);
    }

    public class MarketPriceService : IMarketPriceService
    {
        private readonly IBinanceRestService _binance;
        private readonly ILogger<MarketPriceService> _logger;

    public MarketPriceService(IBinanceRestService binance, ILogger<MarketPriceService> logger)
        {
            _binance = binance;
            _logger = logger;
        }

        public async Task<decimal> GetMarkPriceAsync(string symbol)
        {
            try
            {
                var fresh = await _binance.GetMarkPriceAsync(symbol);
                if (fresh != null)
                {
                    var px = fresh.MarkPrice;
                    if (px <= 0)
                    {
                        _logger.LogWarning("Mark price <= 0 cho {Sym}: {Val}", symbol, px);
                        throw new Exception("Invalid mark price");
                    }
                    // Ngưỡng hợp lý để phát hiện outlier (không clamp) – nếu vượt ngưỡng coi là lỗi tạm thời
                    decimal maxRealistic = symbol.ToUpper().Contains("BTC") ? 200_000m : symbol.ToUpper().Contains("ETH") ? 10_000m : symbol.ToUpper().Contains("BNB") ? 2_000m : 100_000m;
                    if (px > maxRealistic * 5 || px < (symbol.ToUpper().Contains("BTC") ? 100m : symbol.ToUpper().Contains("ETH") ? 10m : symbol.ToUpper().Contains("BNB") ? 1m : 0.001m) / 5)
                    {
                        _logger.LogWarning("Mark price outlier {Sym}: {Val}. Bỏ qua để dùng fallback", symbol, px);
                        throw new Exception("Outlier mark price");
                    }
                    return px;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallback to default mock price for {Symbol}", symbol);
            }
            // Retry with USDT suffix if not present
            if (!(symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase) || symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase)))
            {
                var retrySym = symbol + "USDT";
                try
                {
                    var retry = await _binance.GetMarkPriceAsync(retrySym);
                    if (retry != null) return retry.MarkPrice;
                }
                catch (Exception ex2)
                {
                    _logger.LogWarning(ex2, "Retry fallback failed for {Symbol}", retrySym);
                }
            }
            // Fallback conservative mock
            return symbol.ToUpper() switch
            {
                "BTCUSDT" => 45000m,
                "ETHUSDT" => 3000m,
                "BNBUSDT" => 860m,
                _ => 100m
            };
        }
    }
}