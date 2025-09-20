using InvestDapp.Application.Services.Trading;
using Microsoft.AspNetCore.Mvc;

namespace InvestDapp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class TradingController : ControllerBase
    {
        private readonly InvestDapp.Infrastructure.Services.Binance.IBinanceRestService _binanceService;
        private readonly IInternalOrderService _orderService;
        private readonly ILogger<TradingController> _logger;

        public TradingController(
            InvestDapp.Infrastructure.Services.Binance.IBinanceRestService binanceService,
            IInternalOrderService orderService,
            ILogger<TradingController> logger)
        {
            _binanceService = binanceService;
            _orderService = orderService;
            _logger = logger;
        }

        [HttpGet("symbols")]
        public async Task<IActionResult> GetSymbols()
        {
            try
            {
                var symbols = await _binanceService.GetExchangeInfoAsync();
                
                return Ok(symbols);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting symbols");
                return StatusCode(500, new { error = "Unable to fetch symbols" });
            }
        }

        [HttpGet("klines")]
        public async Task<IActionResult> GetKlines(
            [FromQuery] string symbol, 
            [FromQuery] string interval = "1h", 
            [FromQuery] int limit = 500)
        {
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    return BadRequest(new { error = "Symbol is required" });
                }

                var klines = await _binanceService.GetKlinesAsync(symbol, interval, limit);
                
                return Ok(klines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting klines for {Symbol} {Interval}", symbol, interval);
                return StatusCode(500, new { error = "Unable to fetch klines" });
            }
        }

        [HttpGet("markprice")]
        public async Task<IActionResult> GetMarkPrice([FromQuery] string? symbol = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(symbol))
                {
                    // Single symbol
                    var markPrice = await _binanceService.GetMarkPriceAsync(symbol);
                    if (markPrice != null) return Ok(markPrice);

                    return NotFound(new { error = "Symbol not found" });
                }
                else
                {
                    // All symbols
                    var markPrices = await _binanceService.GetMarkPricesAsync();
                    return Ok(markPrices);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mark price for {Symbol}", symbol);
                return StatusCode(500, new { error = "Unable to fetch mark price" });
            }
        }

        [HttpGet("funding")]
        public async Task<IActionResult> GetFundingRates()
        {
            try
            {
                var fundingRates = await _binanceService.GetFundingRatesAsync();
                return Ok(fundingRates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting funding rates");
                return StatusCode(500, new { error = "Unable to fetch funding rates" });
            }
        }

        [HttpGet("markets")]
        public async Task<IActionResult> GetMarketStats()
        {
            try
            {
                var stats = await _binanceService.Get24hTickerStatsAsync();
                
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting market stats");
                return StatusCode(500, new { error = "Unable to fetch market stats" });
            }
        }
    }
}