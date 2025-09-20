using InvestDapp.Application.Services.Trading;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models.Trading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using InvestDapp.Infrastructure.Data.Config;
namespace InvestDapp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IInternalOrderService _orderService;
        private readonly ITradingRepository? _repo;
        private readonly IOptions<TradingConfig>? _tradingConfig;
        private readonly ILogger<OrderController> _logger;

        // Single constructor: required services first, optional services nullable with defaults
        public OrderController(
            IInternalOrderService orderService,
            ILogger<OrderController> logger,
            ITradingRepository? repo = null,
            IOptions<TradingConfig>? tradingConfig = null)
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
                
                if (result.Status == OrderStatus.Rejected)
                {
                    return BadRequest(new { error = result.Notes ?? "Order rejected" });
                }
                
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
            _logger.LogInformation("Deposit requested by {User} Amount={Amount}", userKey, req.Amount);
            try
            {
                var bal = await _orderService.UpdateUserBalanceAsync(userKey, req.Amount, "DEPOSIT");
                _logger.LogInformation("Deposit processed for {User}: new balance available={Available}", userKey, bal?.AvailableBalance);
                return Ok(bal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deposit for {User} Amount={Amount}", userKey, req.Amount);
                return StatusCode(500, new { error = "Unable to process deposit" });
            }
        }

        [HttpPost("balance/withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] BalanceChangeRequest req)
        {
            if (req.Amount <= 0) return BadRequest(new { error = "Amount must be > 0" });
            var userKey = GetTradingUserKey();
            if (string.IsNullOrEmpty(userKey)) return Unauthorized();
            _logger.LogInformation("Withdraw requested by {User} Amount={Amount}", userKey, req.Amount);
            var bal = await _orderService.GetUserBalanceAsync(userKey);
            if (bal == null)
            {
                _logger.LogWarning("Withdraw attempt but balance record missing for {User}", userKey);
                return BadRequest(new { error = "Insufficient available balance" });
            }
            if (bal.AvailableBalance < req.Amount)
            {
                _logger.LogWarning("Withdraw denied for {User}: available={Available} requested={Requested}", userKey, bal.AvailableBalance, req.Amount);
                return BadRequest(new { error = "Insufficient available balance" });
            }
            try
            {
                InternalUserBalance updated;
                try
                {
                    updated = await _orderService.UpdateUserBalanceAsync(userKey, -req.Amount, "WITHDRAW_REQUEST");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating balance during withdraw request for {User} Amount={Amount}", userKey, req.Amount);
                    return StatusCode(500, new { error = "Unable to reserve funds for withdraw: " + (ex.Message ?? ex.ToString()) });
                }

                if (_repo != null)
                {
                    var recipient = (req.RecipientAddress ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(recipient)) recipient = userKey;
                    if (recipient.Length > 100) recipient = recipient.Substring(0, 100);

                    var wreq = new InvestDapp.Shared.Models.Trading.WalletWithdrawalRequest
                    {
                        UserWallet = userKey,
                        RecipientAddress = recipient,
                        Amount = req.Amount,
                        Status = InvestDapp.Shared.Enums.WithdrawalStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };
                    try
                    {
                        await _repo.AddWalletWithdrawalRequestAsync(wreq);
                        await _repo.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving withdrawal request for {User} Amount={Amount} Recipient={Recipient}", userKey, req.Amount, recipient);
                        try { await _orderService.UpdateUserBalanceAsync(userKey, req.Amount, "WITHDRAW_REQUEST_ROLLBACK"); } catch (Exception rbEx) { _logger.LogError(rbEx, "Rollback failed for {User}", userKey); }
                        var errMsg = ex.InnerException?.Message ?? ex.Message;
                        return StatusCode(500, new { error = "Unable to create withdraw request: " + errMsg });
                    }
                }

                _logger.LogInformation("Withdraw request created for {User}: amount={Amount}", userKey, req.Amount);
                return Ok(new { message = "Withdrawal request created and pending admin approval", balance = updated });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating withdraw request for {User} Amount={Amount}", userKey, req.Amount);
                return StatusCode(500, new { error = "Unable to create withdraw request: " + (ex.Message ?? ex.ToString()) });
            }
        }

        private string? GetTradingUserKey()
        {
            return User.FindFirst("WalletAddress")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}