using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Shared.Models.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace InvestDapp.Application.Services.Trading
{
    public interface IInternalOrderService
    {
        Task<InternalOrder> CreateOrderAsync(InternalOrder order);
        Task<bool> CancelOrderAsync(string orderId, string userId);
        Task<InternalOrder?> GetOrderAsync(string orderId);
        Task<List<InternalOrder>> GetUserOrdersAsync(string userId);
        Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus status);
        Task<InternalPosition?> GetUserPositionAsync(string userId, string symbol);
        Task<List<InternalPosition>> GetUserPositionsAsync(string userId);
        Task<InternalUserBalance?> GetUserBalanceAsync(string userId);
        Task<InternalUserBalance> UpdateUserBalanceAsync(string userId, decimal balanceChange, string reason);
    }

    public class InternalOrderService : IInternalOrderService
    {
        private readonly ConcurrentDictionary<string, InternalOrder> _orders;
        private readonly ConcurrentDictionary<string, InternalPosition> _positions;
        private readonly ConcurrentDictionary<string, InternalUserBalance> _balances;
        private readonly TradingConfig _tradingConfig;
        private readonly ILogger<InternalOrderService> _logger;
        private readonly object _lockObject = new();

        public InternalOrderService(IOptions<TradingConfig> tradingConfig, ILogger<InternalOrderService> logger)
        {
            _orders = new ConcurrentDictionary<string, InternalOrder>();
            _positions = new ConcurrentDictionary<string, InternalPosition>();
            _balances = new ConcurrentDictionary<string, InternalUserBalance>();
            _tradingConfig = tradingConfig.Value;
            _logger = logger;
        }

        public async Task<InternalOrder> CreateOrderAsync(InternalOrder order)
        {
            try
            {
                // Validate order
                var (isValid, errorMessage) = await ValidateOrderAsync(order);
                if (!isValid)
                {
                    order.Status = OrderStatus.Rejected;
                    order.Notes = errorMessage;
                    _orders.TryAdd(order.Id, order);
                    return order;
                }

                // Set initial status
                order.Status = OrderStatus.Pending;
                order.CreatedAt = DateTime.UtcNow;

                // Store order
                _orders.TryAdd(order.Id, order);

                // For market orders, try to fill immediately
                if (order.Type == OrderType.Market)
                {
                    await ProcessMarketOrderAsync(order);
                }

                _logger.LogInformation("Order created: {OrderId} for user {UserId}", order.Id, order.UserId);
                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order {OrderId}", order.Id);
                order.Status = OrderStatus.Rejected;
                order.Notes = "Internal error occurred";
                return order;
            }
        }

        private async Task ProcessMarketOrderAsync(InternalOrder order)
        {
            try
            {
                // Simulate market execution with some randomness
                var random = new Random();
                var executionDelay = random.Next(100, 1000); // 0.1 to 1 second
                await Task.Delay(executionDelay);

                // Mock price execution (in real scenario, this would use order book)
                var executionPrice = await GetCurrentMarketPriceAsync(order.Symbol);
                
                lock (_lockObject)
                {
                    if (_orders.TryGetValue(order.Id, out var currentOrder))
                    {
                        currentOrder.Status = OrderStatus.Filled;
                        currentOrder.FilledQuantity = currentOrder.Quantity;
                        currentOrder.AveragePrice = executionPrice;
                        currentOrder.UpdatedAt = DateTime.UtcNow;
                        _orders[order.Id] = currentOrder;

                        // Update or create position
                        UpdateUserPosition(currentOrder, executionPrice);

                        // Update user balance (deduct margin)
                        var requiredMargin = CalculateRequiredMargin(currentOrder);
                        var balance = GetUserBalanceSync(currentOrder.UserId);
                        if (balance != null)
                        {
                            balance.AvailableBalance -= requiredMargin;
                            balance.UsedMargin += requiredMargin;
                            balance.UpdatedAt = DateTime.UtcNow;
                            _balances[currentOrder.UserId] = balance;
                        }

                        _logger.LogInformation("Market order executed: {OrderId} at price {Price}", 
                            currentOrder.Id, executionPrice);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing market order {OrderId}", order.Id);
                order.Status = OrderStatus.Rejected;
                order.Notes = "Execution failed";
            }
        }

        private void UpdateUserPosition(InternalOrder order, decimal executionPrice)
        {
            var positionKey = $"{order.UserId}:{order.Symbol}";
            
            if (_positions.TryGetValue(positionKey, out var existingPosition))
            {
                // Update existing position
                if (existingPosition.Side == order.Side)
                {
                    // Increase position
                    var totalSize = existingPosition.Size + order.Quantity;
                    var avgPrice = ((existingPosition.Size * existingPosition.EntryPrice) + 
                                   (order.Quantity * executionPrice)) / totalSize;
                    
                    existingPosition.Size = totalSize;
                    existingPosition.EntryPrice = avgPrice;
                    existingPosition.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Reduce position or reverse
                    if (existingPosition.Size > order.Quantity)
                    {
                        existingPosition.Size -= order.Quantity;
                        existingPosition.UpdatedAt = DateTime.UtcNow;
                    }
                    else if (existingPosition.Size == order.Quantity)
                    {
                        // Close position
                        _positions.TryRemove(positionKey, out _);
                        return;
                    }
                    else
                    {
                        // Reverse position
                        existingPosition.Size = order.Quantity - existingPosition.Size;
                        existingPosition.Side = order.Side;
                        existingPosition.EntryPrice = executionPrice;
                        existingPosition.UpdatedAt = DateTime.UtcNow;
                    }
                }
                
                _positions[positionKey] = existingPosition;
            }
            else
            {
                // Create new position
                var newPosition = new InternalPosition
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = order.UserId,
                    Symbol = order.Symbol,
                    Side = order.Side,
                    Size = order.Quantity,
                    EntryPrice = executionPrice,
                    MarkPrice = executionPrice,
                    Leverage = order.Leverage,
                    Margin = CalculateRequiredMargin(order),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                _positions.TryAdd(positionKey, newPosition);
            }
        }

        public async Task<bool> CancelOrderAsync(string orderId, string userId)
        {
            lock (_lockObject)
            {
                if (_orders.TryGetValue(orderId, out var order))
                {
                    if (order.UserId == userId && order.Status == OrderStatus.Pending)
                    {
                        order.Status = OrderStatus.Cancelled;
                        order.UpdatedAt = DateTime.UtcNow;
                        _orders[orderId] = order;
                        return true;
                    }
                }
            }
            return false;
        }

        public async Task<InternalOrder?> GetOrderAsync(string orderId)
        {
            _orders.TryGetValue(orderId, out var order);
            return order;
        }

        public async Task<List<InternalOrder>> GetUserOrdersAsync(string userId)
        {
            lock (_lockObject)
            {
                return _orders.Values.Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedAt).ToList();
            }
        }

        public async Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus status)
        {
            lock (_lockObject)
            {
                if (_orders.TryGetValue(orderId, out var order))
                {
                    if (order.Status != OrderStatus.Filled && order.Status != OrderStatus.Cancelled)
                    {
                        order.Status = status;
                        order.UpdatedAt = DateTime.UtcNow;
                        _orders[orderId] = order;
                        return true;
                    }
                }
            }
            return false;
        }

        public async Task<InternalPosition?> GetUserPositionAsync(string userId, string symbol)
        {
            lock (_lockObject)
            {
                var key = $"{userId}:{symbol}";
                return _positions.TryGetValue(key, out var position) ? position : null;
            }
        }

        public async Task<List<InternalPosition>> GetUserPositionsAsync(string userId)
        {
            lock (_lockObject)
            {
                return _positions.Values.Where(p => p.UserId == userId).ToList();
            }
        }

        public async Task<InternalUserBalance?> GetUserBalanceAsync(string userId)
        {
            lock (_lockObject)
            {
                if (!_balances.TryGetValue(userId, out var balance))
                {
                    // Initialize with default balance
                    balance = new InternalUserBalance
                    {
                        UserId = userId,
                        Balance = _tradingConfig.DefaultBalance,
                        AvailableBalance = _tradingConfig.DefaultBalance
                    };
                    _balances[userId] = balance;
                }
                return balance;
            }
        }

        private InternalUserBalance? GetUserBalanceSync(string userId)
        {
            if (!_balances.TryGetValue(userId, out var balance))
            {
                balance = new InternalUserBalance
                {
                    UserId = userId,
                    Balance = _tradingConfig.DefaultBalance,
                    AvailableBalance = _tradingConfig.DefaultBalance
                };
                _balances[userId] = balance;
            }
            return balance;
        }

        public async Task<InternalUserBalance> UpdateUserBalanceAsync(string userId, decimal balanceChange, string reason)
        {
            lock (_lockObject)
            {
                if (!_balances.TryGetValue(userId, out var balance))
                {
                    balance = new InternalUserBalance
                    {
                        UserId = userId,
                        Balance = _tradingConfig.DefaultBalance,
                        AvailableBalance = _tradingConfig.DefaultBalance
                    };
                }

                balance.Balance += balanceChange;
                balance.AvailableBalance += balanceChange;
                balance.UpdatedAt = DateTime.UtcNow;
                _balances[userId] = balance;

                _logger.LogInformation("Balance updated for user {UserId}: {Change} ({Reason})", userId, balanceChange, reason);
                return balance;
            }
        }

        private async Task<(bool IsValid, string ErrorMessage)> ValidateOrderAsync(InternalOrder order)
        {
            // Check minimum order size
            if (order.Quantity < _tradingConfig.MinOrderSize)
            {
                return (false, $"Order quantity too small. Minimum: {_tradingConfig.MinOrderSize}");
            }

            // Check maximum order size
            if (order.Quantity > _tradingConfig.MaxOrderSize)
            {
                return (false, $"Order quantity too large. Maximum: {_tradingConfig.MaxOrderSize}");
            }

            // Check user balance
            var balance = await GetUserBalanceAsync(order.UserId);
            if (balance != null)
            {
                var requiredMargin = CalculateRequiredMargin(order);
                if (balance.AvailableBalance < requiredMargin)
                {
                    return (false, "Insufficient balance");
                }
            }

            // Check leverage limits
            var maxLeverage = _tradingConfig.MaxLeveragePerSymbol.TryGetValue(order.Symbol, out var leverage) 
                ? leverage 
                : _tradingConfig.MaxLeveragePerSymbol["Default"];
            
            if (order.Leverage > maxLeverage)
            {
                return (false, $"Leverage too high. Maximum for {order.Symbol}: {maxLeverage}x");
            }

            return (true, string.Empty);
        }

        private decimal CalculateRequiredMargin(InternalOrder order)
        {
            var mockPrice = GetCurrentMarketPriceSync(order.Symbol);
            var notionalValue = order.Quantity * (order.Price ?? mockPrice);
            return notionalValue / order.Leverage;
        }

        private decimal GetCurrentMarketPriceSync(string symbol)
        {
            // Mock price - in real scenario, this would get from market data
            return symbol.ToUpper() switch
            {
                "BTCUSDT" => 45000m + (decimal)(new Random().NextDouble() * 1000 - 500),
                "ETHUSDT" => 3000m + (decimal)(new Random().NextDouble() * 200 - 100),
                "BNBUSDT" => 400m + (decimal)(new Random().NextDouble() * 50 - 25),
                _ => 100m
            };
        }

        private async Task<decimal> GetCurrentMarketPriceAsync(string symbol)
        {
            // Mock price - in real scenario, this would get from market data
            return await Task.FromResult(GetCurrentMarketPriceSync(symbol));
        }
    }
}