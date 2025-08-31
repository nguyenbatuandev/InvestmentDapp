using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System;
using InvestDapp.Shared.Models.Trading;

namespace InvestDapp.Application.Services.Trading
{
    public class TradingEngine : BackgroundService
    {
        private readonly ILogger<TradingEngine> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private const int IntervalMs = 1500; // 1.5s loop

        public TradingEngine(ILogger<TradingEngine> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TradingEngine started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPositionsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TradingEngine loop error");
                }
                await Task.Delay(IntervalMs, stoppingToken);
            }
            _logger.LogInformation("TradingEngine stopped");
        }

        private async Task ProcessPositionsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IInternalOrderService>();
            var priceService = scope.ServiceProvider.GetRequiredService<IMarketPriceService>();

            var positions = orderService.GetAllPositions();
            // Execute conditional (limit/stop) orders first
            if (orderService is InternalOrderService ios)
            {
                var pending = ios.GetOpenConditionalOrders();
                foreach (var pendingOrd in pending.ToList())
                {
                    decimal mp;
                    try { mp = await priceService.GetMarkPriceAsync(pendingOrd.Symbol); } catch { continue; }
                    await ios.TryExecuteConditionalAsync(pendingOrd.Id, mp);
                }
            }
            foreach (var pos in positions.ToList()) // snapshot
            {
                if (ct.IsCancellationRequested) break;
                decimal markPrice;
                try { markPrice = await priceService.GetMarkPriceAsync(pos.Symbol); }
                catch { continue; }

                // Simple PnL calc
                var priceDiff = (markPrice - pos.EntryPrice) * (pos.Side == Shared.Models.Trading.OrderSide.Buy ? 1 : -1);
                var unrealized = priceDiff * pos.Size;

                bool hitTp = pos.TakeProfitPrice.HasValue && ((pos.Side == Shared.Models.Trading.OrderSide.Buy && markPrice >= pos.TakeProfitPrice) || (pos.Side == Shared.Models.Trading.OrderSide.Sell && markPrice <= pos.TakeProfitPrice));
                bool hitSl = pos.StopLossPrice.HasValue && ((pos.Side == Shared.Models.Trading.OrderSide.Buy && markPrice <= pos.StopLossPrice) || (pos.Side == Shared.Models.Trading.OrderSide.Sell && markPrice >= pos.StopLossPrice));

                // Liquidation check
                bool shouldLiquidate = false;
                decimal? liqPrice = pos.LiquidationPrice;
                if (liqPrice.HasValue)
                {
                    if (pos.Side == OrderSide.Buy && markPrice <= liqPrice.Value) shouldLiquidate = true;
                    if (pos.Side == OrderSide.Sell && markPrice >= liqPrice.Value) shouldLiquidate = true;
                }

                if (hitTp || hitSl || shouldLiquidate)
                {
                    if (shouldLiquidate)
                        _logger.LogWarning("LIQUIDATION position {User}:{Sym} mark={Mark} liq={Liq} side={Side}", pos.UserId, pos.Symbol, markPrice, liqPrice, pos.Side);
                    else
                        _logger.LogInformation("Auto-closing position {User}:{Sym} TP/SL hit (TP={TP} SL={SL} Mark={Mark})", pos.UserId, pos.Symbol, pos.TakeProfitPrice, pos.StopLossPrice, markPrice);
                    // Create opposite market order to close
                    var closeOrder = new InternalOrder
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = pos.UserId,
                        Symbol = pos.Symbol,
                        Side = pos.Side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy,
                        Type = OrderType.Market,
                        Quantity = pos.Size,
                        Leverage = pos.Leverage,
                        ReduceOnly = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await orderService.CreateOrderAsync(closeOrder);
                }
            }
        }
    }
}
