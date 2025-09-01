
using InvestDapp.Application.Services.Trading;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models.Trading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InvestDapp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IInternalOrderService _orderService;
        private readonly ITradingRepository? _repo;
        private readonly Microsoft.Extensions.Options.IOptions<InvestDapp.Infrastructure.Data.Config.TradingConfig>? _tradingConfig;
        private readonly ILogger<OrderController> _logger;

        // Single constructor: required services first, optional services nullable with defaults
        public OrderController(
            IInternalOrderService orderService,
            ILogger<OrderController> logger,
            ITradingRepository? repo = null,
            Microsoft.Extensions.Options.IOptions<InvestDapp.Infrastructure.Data.Config.TradingConfig>? tradingConfig = null)
        {
            _orderService = orderService;
            _logger = logger;
            _repo = repo;
            _tradingConfig = tradingConfig;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                var userKey = GetTradingUserKey();
                if (string.IsNullOrEmpty(userKey))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                var order = new InternalOrder
                {
                    UserId = userKey,
                    Symbol = request.Symbol,
                    Side = request.Side,
                    Type = request.Type,
                    Quantity = request.Quantity,
                    Price = request.Price,
                    StopPrice = request.StopPrice,
                    Leverage = request.Leverage,
                    TakeProfitPrice = request.TakeProfitPrice,
                    StopLossPrice = request.StopLossPrice,
                    ReduceOnly = request.ReduceOnly
                };

                var result = await _orderService.CreateOrderAsync(order);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, new { error = "Unable to create order" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserOrders()
        {
            try
            {
                var userKey = GetTradingUserKey();
                if (string.IsNullOrEmpty(userKey))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                var orders = await _orderService.GetUserOrdersAsync(userKey);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user orders");
                return StatusCode(500, new { error = "Unable to fetch orders" });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetOrderHistory()
        {
            try
            {
                var userWallet = User?.FindFirst("WalletAddress")?.Value;
                _logger.LogInformation("Getting order history for wallet: {Wallet}", userWallet);
                var orders = await _orderService.GetUserOrdersAsync(userWallet);
                _logger.LogInformation("Found {Count} orders for wallet {Wallet}", orders.Count, userWallet);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order history");
                return StatusCode(500, new { error = "Unable to fetch order history" });
            }
        }


        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrder(string orderId)
        {
            try
            {
                var order = await _orderService.GetOrderAsync(orderId);
                if (order == null)
                {
                    return NotFound(new { error = "Order not found" });
                }

                var userKey = GetTradingUserKey();
                if (order.UserId != userKey)
                {
                    return Forbid();
                }

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId}", orderId);
                return StatusCode(500, new { error = "Unable to fetch order" });
            }
        }

        [HttpDelete("{orderId}")]
        public async Task<IActionResult> CancelOrder(string orderId)
        {
            try
            {
                var userKey = GetTradingUserKey();
                if (string.IsNullOrEmpty(userKey))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                var success = await _orderService.CancelOrderAsync(orderId, userKey);
                if (!success)
                {
                    return BadRequest(new { error = "Unable to cancel order" });
                }

                return Ok(new { message = "Order cancelled successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
                return StatusCode(500, new { error = "Unable to cancel order" });
            }
        }

        [HttpGet("positions")]
        public async Task<IActionResult> GetUserPositions()
        {
            try
            {
                var userKey = GetTradingUserKey();
                if (string.IsNullOrEmpty(userKey))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                var positions = await _orderService.GetUserPositionsAsync(userKey);
                return Ok(positions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user positions");
                return StatusCode(500, new { error = "Unable to fetch positions" });
            }
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetUserBalance()
        {
            try
            {
                var userKey = GetTradingUserKey();
                if (string.IsNullOrEmpty(userKey))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                var balance = await _orderService.GetUserBalanceAsync(userKey);
                return Ok(balance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user balance");
                return StatusCode(500, new { error = "Unable to fetch balance" });
            }
        }

        [HttpGet("balance/transactions")]
        public IActionResult GetBalanceTransactions()
        {
            var userKey = GetTradingUserKey();
            if (string.IsNullOrEmpty(userKey)) return Unauthorized();
            // Simple recent 100 tx
            // Direct DB context not injected here; for quick access move to service later if needed
            // Use repository via DI (if available)
            if (_repo == null) return Ok("TODO: implement via service (needs db context)");
            var tx = _repo.GetUserBalanceAsync(userKey).GetAwaiter().GetResult();
            return Ok(tx);
        }

        [HttpGet("portfolio")]
        public async Task<IActionResult> GetPortfolio([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var userKey = GetTradingUserKey();
            if (string.IsNullOrEmpty(userKey)) return Unauthorized();

            try
            {
                // Load orders, positions and balance transactions
                var orders = await _repo.GetOrdersByUserAsync(userKey);
                var positions = await _repo.GetPositionsByUserAsync(userKey);
                // Load balance txs from DB directly via context (BalanceTransactions DbSet exists)
                var ctx = (InvestDapp.Infrastructure.Data.InvestDbContext?)null;
                try { ctx = (InvestDapp.Infrastructure.Data.InvestDbContext?)HttpContext.RequestServices.GetService(typeof(InvestDapp.Infrastructure.Data.InvestDbContext)); } catch { ctx = null; }

                List<InvestDapp.Shared.Models.Trading.BalanceTransaction> txs = new();
                if (ctx != null)
                {
                    var q = ctx.BalanceTransactions.Where(t => t.UserWallet == userKey).AsQueryable();
                    if (from.HasValue) q = q.Where(t => t.CreatedAt >= from.Value);
                    if (to.HasValue) q = q.Where(t => t.CreatedAt <= to.Value);
                    txs = q.OrderByDescending(t => t.CreatedAt).Take(1000).ToList();
                }

                // Aggregate PnL and fees and also build a list of completed trades for the frontend
                decimal realizedPnl = 0m;
                decimal totalFees = 0m;

                var completedTrades = new List<object>();
                var feeRateFraction = (_tradingConfig?.Value?.FeeRatePercent ?? 0.04m) / 100m;

                // Robust FIFO matcher: for each symbol, match filled orders into trades (open vs close) using chronological FIFO
                // Include orders that are filled and have either AvgPrice (recorded fill price) or a Price fallback
                var filledOrders = orders.Where(o => o.Status == InvestDapp.Shared.Models.Trading.OrderStatus.Filled && o.FilledQuantity > 0 && (o.AvgPrice > 0 || (o.Price.HasValue && o.Price.Value > 0)))
                                         .OrderBy(o => o.CreatedAt)
                                         .ToList();

                var groups = filledOrders.GroupBy(o => o.Symbol);
                foreach (var g in groups)
                {
                    // Queues for open longs (buys) and open shorts (sells)
                    var openLongs = new Queue<(decimal Remaining, decimal Price, DateTime CreatedAt)>();
                    var openShorts = new Queue<(decimal Remaining, decimal Price, DateTime CreatedAt)>();

                    foreach (var o in g)
                    {
                        var remaining = o.FilledQuantity;
                        // effective execution price: prefer AvgPrice, fall back to declared Price if AvgPrice missing
                        var execPrice = o.AvgPrice > 0 ? o.AvgPrice : (o.Price ?? 0m);
                        if (o.Side == InvestDapp.Shared.Models.Trading.OrderSide.Buy)
                        {
                            // Buy: first attempt to close existing shorts
                            while (remaining > 0 && openShorts.Count > 0)
                            {
                                var head = openShorts.Peek();
                                var matched = Math.Min(remaining, head.Remaining);
                                var entryPrice = head.Price; // short entry
                                var exitPrice = execPrice; // buy to close
                                var pnl = (entryPrice - exitPrice) * matched; // short profit = entry - exit
                                var fees = (entryPrice * matched + exitPrice * matched) * feeRateFraction;
                                totalFees += fees;
                                completedTrades.Add(new { id = o.Id, symbol = o.Symbol, side = "SHORT", qty = matched, entryPrice, exitPrice, entryTs = head.CreatedAt, exitTs = o.CreatedAt, pnl, fees, note = "Backend Trade" });

                                // decrement
                                remaining -= matched;
                                // consume head
                                openShorts.Dequeue();
                                if (matched < head.Remaining)
                                {
                                    // push remaining portion back to front (preserve FIFO)
                                    var newHead = (head.Remaining - matched, head.Price, head.CreatedAt);
                                    var temp = new Queue<(decimal Remaining, decimal Price, DateTime CreatedAt)>();
                                    temp.Enqueue(newHead);
                                    while (openShorts.Count > 0) temp.Enqueue(openShorts.Dequeue());
                                    openShorts = temp;
                                }
                            }

                            // any leftover becomes a new open long
                            if (remaining > 0) openLongs.Enqueue((remaining, execPrice, o.CreatedAt));
                        }
                        else // Sell
                        {
                            // Sell: first attempt to close existing longs
                            while (remaining > 0 && openLongs.Count > 0)
                            {
                                var head = openLongs.Peek();
                                var matched = Math.Min(remaining, head.Remaining);
                                var entryPrice = head.Price; // long entry
                                var exitPrice = execPrice; // sell to close
                                var pnl = (exitPrice - entryPrice) * matched; // long profit
                                var fees = (entryPrice * matched + exitPrice * matched) * feeRateFraction;
                                totalFees += fees;
                                completedTrades.Add(new { id = o.Id, symbol = o.Symbol, side = "LONG", qty = matched, entryPrice, exitPrice, entryTs = head.CreatedAt, exitTs = o.CreatedAt, pnl, fees, note = "Backend Trade" });

                                remaining -= matched;
                                // consume head
                                openLongs.Dequeue();
                                if (matched < head.Remaining)
                                {
                                    var newHead = (head.Remaining - matched, head.Price, head.CreatedAt);
                                    var temp = new Queue<(decimal Remaining, decimal Price, DateTime CreatedAt)>();
                                    temp.Enqueue(newHead);
                                    while (openLongs.Count > 0) temp.Enqueue(openLongs.Dequeue());
                                    openLongs = temp;
                                }
                            }

                            // any leftover becomes a new open short
                            if (remaining > 0) openShorts.Enqueue((remaining, execPrice, o.CreatedAt));
                        }
                    }
                }

                // Sum realized pnl from positions table (open positions) plus completed trades
                var realizedFromPositions = positions.Sum(p => p.RealizedPnl);
                var realizedFromTrades = completedTrades.Sum(t =>
                {
                    var prop = t.GetType().GetProperty("pnl");
                    if (prop == null) return 0m;
                    return Convert.ToDecimal(prop.GetValue(t) ?? 0m);
                });
                realizedPnl = realizedFromPositions + realizedFromTrades;

                // Build simple cumulative PnL time-series from completedTrades by exit date
                var timeSeries = new List<object>();
                decimal cumulative = 0m;
                foreach (var g in completedTrades.OrderBy(t => ((DateTime) (t.GetType().GetProperty("exitTs")?.GetValue(t) ?? DateTime.UtcNow))).GroupBy(t => ((DateTime) (t.GetType().GetProperty("exitTs")?.GetValue(t) ?? DateTime.UtcNow)).Date))
                {
                    foreach (var item in g)
                    {
                        var pnlProp = item.GetType().GetProperty("pnl");
                        if (pnlProp != null)
                        {
                            cumulative += Convert.ToDecimal(pnlProp.GetValue(item) ?? 0m);
                        }
                    }
                    var date = g.Key;
                    timeSeries.Add(new { time = date.ToString("yyyy-MM-dd"), value = cumulative });
                }

                var bal = await _orderService.GetUserBalanceAsync(userKey);

                // normalize balance to a simple numeric value for frontend
                decimal numericBalance = 0m;
                if (bal != null)
                {
                    numericBalance = bal.Balance != 0m ? bal.Balance : bal.AvailableBalance;
                }

                var result = new
                {
                    user = userKey,
                    balance = numericBalance,
                    realizedPnl,
                    totalFees,
                    ordersCount = orders.Count,
                    positionsCount = positions.Count,
                    // raw DB balance transactions
                    recentTransactions = txs,
                    // synthetic trade list derived from orders
                    transactions = completedTrades,
                    // time series for charting
                    timeSeries = timeSeries
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building portfolio for {User}", userKey);
                return StatusCode(500, new { error = "Unable to build portfolio" });
            }
        }

        [HttpPost("positions/risk")]
        public async Task<IActionResult> UpdatePositionRisk([FromBody] UpdatePositionRiskRequest request)
        {
            try
            {
                var userId = GetTradingUserKey();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }
                var ok = await _orderService.UpdatePositionRiskAsync(userId, request.Symbol, request.TakeProfitPrice, request.StopLossPrice, request.PositionId);
                if (!ok)
                {
                    return BadRequest(new { error = "Unable to update TP/SL for position" });
                }
                return Ok(new { message = "Updated", symbol = request.Symbol, takeProfit = request.TakeProfitPrice, stopLoss = request.StopLossPrice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating position risk for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = "Internal error" });
            }
        }

        [HttpPost("positions/close")]
        public async Task<IActionResult> ClosePosition([FromBody] ClosePositionRequest req)
        {
            try
            {
                var userId = GetTradingUserKey();
                if (string.IsNullOrEmpty(userId)) return Unauthorized(new { error = "User not authenticated" });
                if (string.IsNullOrWhiteSpace(req.Symbol)) return BadRequest(new { error = "Symbol required" });
                var (ok, error) = await _orderService.ClosePositionAsync(userId, req.Symbol, req.PositionId);
                if (!ok) return BadRequest(new { error });
                return Ok(new { message = "Position close order submitted", symbol = req.Symbol });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing position {Sym}", req.Symbol);
                return StatusCode(500, new { error = "Internal error" });
            }
        }

        [HttpPost("balance/deposit")]
        public async Task<IActionResult> Deposit([FromBody] BalanceChangeRequest req)
        {
            if (req.Amount <= 0) return BadRequest(new { error = "Amount must be > 0" });
            var userKey = GetTradingUserKey();
            if (string.IsNullOrEmpty(userKey)) return Unauthorized();
            var bal = await _orderService.UpdateUserBalanceAsync(userKey, req.Amount, "DEPOSIT");
            return Ok(bal);
        }

        [HttpPost("balance/withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] BalanceChangeRequest req)
        {
            if (req.Amount <= 0) return BadRequest(new { error = "Amount must be > 0" });
            var userKey = GetTradingUserKey();
            if (string.IsNullOrEmpty(userKey)) return Unauthorized();
            var bal = await _orderService.GetUserBalanceAsync(userKey);
            if (bal == null || bal.AvailableBalance < req.Amount) return BadRequest(new { error = "Insufficient available balance" });
            var updated = await _orderService.UpdateUserBalanceAsync(userKey, -req.Amount, "WITHDRAW");
            return Ok(updated);
        }

        private string? GetTradingUserKey()
        {
            return User.FindFirst("WalletAddress")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}