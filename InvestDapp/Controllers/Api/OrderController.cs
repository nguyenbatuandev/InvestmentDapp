
using InvestDapp.Application.Services.Trading;
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
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            IInternalOrderService orderService,
            ILogger<OrderController> logger)
        {
            _orderService = orderService;
            _logger = logger;
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
                    UserId = userKey, // use wallet address as trading key
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

        // Debug endpoint without auth
        [HttpGet("debug/history")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOrderHistory()
        {
            try
            {
                // Lấy wallet từ session hoặc claims nếu có
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

        // Debug endpoint to create sample orders
        [HttpPost("debug/create-sample")]
        [AllowAnonymous]
        public async Task<IActionResult> CreateSampleOrders()
        {
            try
            {
                var testUserId = "test-user-123";
                
                // Create some sample filled orders for testing
                var sampleOrders = new[]
                {
                    new InternalOrder
                    {
                        UserId = testUserId,
                        Symbol = "BTCUSDT",
                        Side = OrderSide.Buy,
                        Type = OrderType.Market,
                        Quantity = 0.001m,
                        Price = 108315.00m,
                        Status = OrderStatus.Filled,
                        FilledQuantity = 0.001m,
                        AveragePrice = 108315.00m,
                        Leverage = 10,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                        UpdatedAt = DateTime.UtcNow.AddMinutes(-25)
                    },
                    new InternalOrder
                    {
                        UserId = testUserId,
                        Symbol = "BNBUSDT", 
                        Side = OrderSide.Sell,
                        Type = OrderType.Market,
                        Quantity = 0.05m,
                        Price = 860.00m,
                        Status = OrderStatus.Filled,
                        FilledQuantity = 0.05m,
                        AveragePrice = 858.50m,
                        Leverage = 5,
                        CreatedAt = DateTime.UtcNow.AddHours(-2),
                        UpdatedAt = DateTime.UtcNow.AddHours(-1)
                    },
                    new InternalOrder
                    {
                        UserId = testUserId,
                        Symbol = "ETHUSDT",
                        Side = OrderSide.Buy,
                        Type = OrderType.Limit,
                        Quantity = 0.1m,
                        Price = 4200.00m,
                        Status = OrderStatus.Filled,
                        FilledQuantity = 0.1m,
                        AveragePrice = 4195.50m,
                        Leverage = 3,
                        CreatedAt = DateTime.UtcNow.AddDays(-1),
                        UpdatedAt = DateTime.UtcNow.AddDays(-1).AddMinutes(15)
                    }
                };

                var createdOrders = new List<InternalOrder>();
                foreach (var order in sampleOrders)
                {
                    var created = await _orderService.CreateOrderAsync(order);
                    createdOrders.Add(created);
                }

                return Ok(new { message = "Sample orders created", count = createdOrders.Count, orders = createdOrders });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sample orders");
                return StatusCode(500, new { error = "Unable to create sample orders" });
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

        // Debug helper: orders + positions snapshot
        [HttpGet("debug/snapshot")]
        public async Task<IActionResult> Snapshot()
        {
            try
            {
                var userKey = GetTradingUserKey();
                if (string.IsNullOrEmpty(userKey)) return Unauthorized(new { error = "User not authenticated" });
                var orders = await _orderService.GetUserOrdersAsync(userKey);
                var positions = await _orderService.GetUserPositionsAsync(userKey);
                var balance = await _orderService.GetUserBalanceAsync(userKey);
                return Ok(new { orders, positions, balance });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Snapshot error");
                return StatusCode(500, new { error = "Internal snapshot error" });
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
            return Ok("TODO: implement via service (needs db context)");
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
                var ok = await _orderService.UpdatePositionRiskAsync(userId, request.Symbol, request.TakeProfitPrice, request.StopLossPrice);
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
                var (ok, error) = await _orderService.ClosePositionAsync(userId, req.Symbol);
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
            // Prefer wallet address for trading identity; fallback to numeric user id
            return User.FindFirst("WalletAddress")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }

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
    }

    public class BalanceChangeRequest
    {
        public decimal Amount { get; set; }
    }

    public class ClosePositionRequest
    {
        public string Symbol { get; set; } = string.Empty;
    }
}