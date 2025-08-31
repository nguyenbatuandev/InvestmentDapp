using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Models.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Linq;

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
        IReadOnlyCollection<InternalPosition> GetAllPositions();
    Task<bool> UpdatePositionRiskAsync(string userId, string symbol, decimal? takeProfit, decimal? stopLoss);
    Task<(bool ok, string? error)> ClosePositionAsync(string userId, string symbol);
    }

    public class InternalOrderService : IInternalOrderService
    {
        private readonly ConcurrentDictionary<string, InternalOrder> _orders;
        private readonly ConcurrentDictionary<string, InternalPosition> _positions;
        private readonly ConcurrentDictionary<string, InternalUserBalance> _balances;
    // Track pending conditional (non-market) orders
    private readonly ConcurrentDictionary<string, InternalOrder> _openConditionalOrders = new();
    private readonly TradingConfig _tradingConfig;
    private readonly InvestDbContext _db;
    private readonly IMarketPriceService _priceService;
        private readonly ILogger<InternalOrderService> _logger;
        private readonly object _lockObject = new();

        public InternalOrderService(IOptions<TradingConfig> tradingConfig,
            ILogger<InternalOrderService> logger,
            InvestDbContext db,
            IMarketPriceService priceService)
        {
            _orders = new ConcurrentDictionary<string, InternalOrder>();
            _positions = new ConcurrentDictionary<string, InternalPosition>();
            _balances = new ConcurrentDictionary<string, InternalUserBalance>();
            _tradingConfig = tradingConfig.Value;
            _logger = logger;
            _db = db;
            _priceService = priceService;
        }

        public async Task<InternalOrder> CreateOrderAsync(InternalOrder order)
        {
            try
            {
                // Normalize symbol to standard (e.g., BNB -> BNBUSDT)
                order.Symbol = NormalizeSymbol(order.Symbol);
                var (isValid, errorMessage) = await ValidateOrderAsync(order);
                if (!isValid)
                {
                    order.Status = OrderStatus.Rejected;
                    order.Notes = errorMessage;
                    _orders.TryAdd(order.Id, order);
                    return order;
                }

                order.Status = OrderStatus.Pending;
                order.CreatedAt = DateTime.UtcNow; // single timestamp reused for DB row to allow later matching

                _orders.TryAdd(order.Id, order);
                _db.Orders.Add(new Order
                {
                    InternalOrderId = order.Id,
                    UserWallet = order.UserId,
                    Symbol = order.Symbol,
                    Side = order.Side,
                    Type = order.Type,
                    Quantity = order.Quantity,
                    Price = order.Price,
                    StopPrice = order.StopPrice,
                    TakeProfitPrice = order.TakeProfitPrice,
                    StopLossPrice = order.StopLossPrice,
                    ReduceOnly = order.ReduceOnly,
                    Leverage = order.Leverage,
                    Status = OrderStatus.Pending,
                    CreatedAt = order.CreatedAt // keep identical for later lookup
                });
                await _db.SaveChangesAsync();

                if (order.Type == OrderType.Market)
                {
                    // Pre-reserve margin immediately so UI sees reduced available balance right after placing order
                    try
                    {
                        if (!order.ReduceOnly) // do not reserve margin for reduce-only close orders
                        {
                            var requiredMargin = CalculateRequiredMargin(order);
                            var bal = GetUserBalanceSync(order.UserId);
                            if (bal != null && requiredMargin > 0)
                            {
                                bal.AvailableBalance -= requiredMargin;
                                bal.UsedMargin += requiredMargin;
                                bal.UpdatedAt = DateTime.UtcNow;
                                // Persist provisional margin reservation
                                var dbBalEarly = _db.UserBalances.FirstOrDefault(b => b.UserWallet == order.UserId);
                                if (dbBalEarly == null)
                                {
                                    dbBalEarly = new UserBalance
                                    {
                                        UserWallet = order.UserId,
                                        Balance = bal.Balance,
                                        AvailableBalance = bal.AvailableBalance,
                                        MarginUsed = bal.UsedMargin,
                                        UnrealizedPnl = bal.UnrealizedPnl,
                                        UpdatedAt = bal.UpdatedAt
                                    };
                                    _db.UserBalances.Add(dbBalEarly);
                                }
                                else
                                {
                                    dbBalEarly.Balance = bal.Balance;
                                    dbBalEarly.AvailableBalance = bal.AvailableBalance;
                                    dbBalEarly.MarginUsed = bal.UsedMargin;
                                    dbBalEarly.UnrealizedPnl = bal.UnrealizedPnl;
                                    dbBalEarly.UpdatedAt = bal.UpdatedAt;
                                }
                                _db.SaveChanges();
                            }
                        }
                    }
                    catch { /* soft fail margin reservation */ }
                    await ProcessMarketOrderAsync(order);
                }
                else
                {
                    // Index pending limit/stop order, margin not reserved until execution (simpler)
                    IndexConditionalOrder(order);
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

        private string NormalizeSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return symbol;
            symbol = symbol.Trim().ToUpper();
            // Remove common separators (e.g., BNB/USDT, BNB-USDT)
            symbol = symbol.Replace("/", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
            if (symbol.EndsWith("USDT") || symbol.EndsWith("USD")) return symbol;
            // Append USDT if only base provided (<=6 chars heuristically)
            if (symbol.Length <= 6) return symbol + "USDT";
            return symbol;
        }

    private async Task ProcessMarketOrderAsync(InternalOrder order, decimal? forcedExecutionPrice = null)
        {
            decimal executionPrice;
            if (forcedExecutionPrice.HasValue)
            {
                executionPrice = forcedExecutionPrice.Value;
            }
            else
            {
                try { executionPrice = await _priceService.GetMarkPriceAsync(order.Symbol); }
                catch { executionPrice = await _priceService.GetMarkPriceAsync(order.Symbol); } // try again fallback inside service
            }

            // Kiểm tra giá bất thường thay vì clamp về hardCap (tránh Entry luôn 2,000,000)
            decimal realisticMax = order.Symbol.Contains("BTC") ? 200_000m : order.Symbol.Contains("ETH") ? 10_000m : order.Symbol.Contains("BNB") ? 2_000m : 100_000m;
            decimal realisticMin = order.Symbol.Contains("BTC") ? 100m : order.Symbol.Contains("ETH") ? 10m : order.Symbol.Contains("BNB") ? 1m : 0.001m;
            if (executionPrice <= 0 || executionPrice > realisticMax * 5 || executionPrice < realisticMin / 5)
            {
                _logger.LogWarning("Execution price OUTLIER {Sym}: {Price}. Refetching...", order.Symbol, executionPrice);
                try
                {
                    var retry = await _priceService.GetMarkPriceAsync(order.Symbol);
                    if (retry > 0 && retry <= realisticMax * 5 && retry >= realisticMin / 5)
                        executionPrice = retry;
                }
                catch { }
            }
            // Nếu vẫn ngoài ngưỡng — reject order
            if (executionPrice <= 0 || executionPrice > realisticMax * 5 || executionPrice < realisticMin / 5)
            {
                order.Status = OrderStatus.Rejected;
                order.Notes = "Giá thị trường bất thường. Thử lại sau";
                _logger.LogWarning("Reject order {Id} do giá bất thường {Price} {Sym}", order.Id, executionPrice, order.Symbol);
                return; // không fill
            }

            // If limit order carried an extreme Price/StopPrice adjust similarly
            if (order.Price.HasValue && (order.Price <= 0 || order.Price > 1_000_000_000m))
            {
                _logger.LogWarning("Order price invalid for {Sym}: {Price}. Nulling.", order.Symbol, order.Price);
                order.Price = null;
            }
            if (order.StopPrice.HasValue && (order.StopPrice <= 0 || order.StopPrice > 1_000_000_000m))
            {
                _logger.LogWarning("Order stop price invalid for {Sym}: {Price}. Nulling.", order.Symbol, order.StopPrice);
                order.StopPrice = null;
            }

            lock (_lockObject)
            {
                if (!_orders.TryGetValue(order.Id, out var currentOrder)) return;
                currentOrder.Status = OrderStatus.Filled;
                currentOrder.FilledQuantity = currentOrder.Quantity;
                // Ensure sanitized execution price (already clamped). Force rounding to 2 decimals for display and DB consistency.
                executionPrice = Math.Round(executionPrice, 2, MidpointRounding.AwayFromZero);
                currentOrder.AveragePrice = executionPrice;
                currentOrder.UpdatedAt = DateTime.UtcNow;
                _orders[order.Id] = currentOrder;

                var dbOrder = _db.Orders.FirstOrDefault(o => o.UserWallet == currentOrder.UserId && o.Symbol == currentOrder.Symbol && o.CreatedAt == currentOrder.CreatedAt);
                if (dbOrder != null)
                {
                    dbOrder.Status = OrderStatus.Filled;
                    dbOrder.FilledQuantity = currentOrder.FilledQuantity;
                    dbOrder.AvgPrice = currentOrder.AveragePrice;
                    dbOrder.UpdatedAt = DateTime.UtcNow;
                }

                // Update position & balances (includes realized PnL + margin adjustments)
                UpdateUserPosition(currentOrder, executionPrice);

                // Persist balance after position update
                var balance = GetUserBalanceSync(currentOrder.UserId);
                if (balance != null)
                {
                    var dbBalance = _db.UserBalances.FirstOrDefault(b => b.UserWallet == currentOrder.UserId);
                    if (dbBalance == null)
                    {
                        dbBalance = new UserBalance
                        {
                            UserWallet = currentOrder.UserId,
                            Balance = balance.Balance,
                            AvailableBalance = balance.AvailableBalance,
                            MarginUsed = balance.UsedMargin,
                            UnrealizedPnl = balance.UnrealizedPnl,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _db.UserBalances.Add(dbBalance);
                    }
                    else
                    {
                        dbBalance.Balance = balance.Balance;
                        dbBalance.AvailableBalance = balance.AvailableBalance;
                        dbBalance.MarginUsed = balance.UsedMargin;
                        dbBalance.UnrealizedPnl = balance.UnrealizedPnl;
                        dbBalance.UpdatedAt = DateTime.UtcNow;
                    }
                }

                // Persist modified/removed position
                var posKey = $"{currentOrder.UserId}:{currentOrder.Symbol}";
                if (_positions.TryGetValue(posKey, out var memPos))
                {
                    UpsertDbPosition(memPos);
                }
                else
                {
                    // Removed => delete from DB if exists
                    var existingDbPos = _db.Positions.FirstOrDefault(x => x.UserWallet == currentOrder.UserId && x.Symbol == currentOrder.Symbol);
                    if (existingDbPos != null)
                    {
                        _db.Positions.Remove(existingDbPos);
                    }
                }
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Market order executed: {OrderId} at price {Price}", order.Id, executionPrice);
        }

        private void UpdateUserPosition(InternalOrder order, decimal executionPrice)
        {
            var positionKey = $"{order.UserId}:{order.Symbol}";
            var balance = GetUserBalanceSync(order.UserId)!; // ensured non-null

        if (_positions.TryGetValue(positionKey, out var pos))
            {
                // Nếu là reduce-only, chắc chắn chỉ đóng hoặc đóng một phần, không đảo chiều
                if (order.ReduceOnly && order.Quantity > pos.Size)
                {
                    _logger.LogInformation("Clamp reduce-only execution qty from {Q} to {Size} for user {U} {Sym}", order.Quantity, pos.Size, order.UserId, order.Symbol);
                    order.Quantity = pos.Size;
                }
                // Existing position
                if (pos.Side == order.Side && !order.ReduceOnly)
                {
                    // Increase (add) to position
                    var newTotalSize = pos.Size + order.Quantity;
                    var newEntry = ((pos.EntryPrice * pos.Size) + (executionPrice * order.Quantity)) / newTotalSize;
                    // Recalculate margin
                    var oldMargin = pos.Margin;
                    pos.Size = newTotalSize;
                    pos.EntryPrice = newEntry;
                    pos.Margin = (pos.Size * pos.EntryPrice) / Math.Max(1, pos.Leverage);
                    var marginIncrease = pos.Margin - oldMargin;
                    if (marginIncrease > 0)
                    {
                        balance.AvailableBalance -= marginIncrease;
                        balance.UsedMargin += marginIncrease;
                    }
            // If user supplies new TP/SL override existing
            if (order.TakeProfitPrice.HasValue) pos.TakeProfitPrice = order.TakeProfitPrice;
            if (order.StopLossPrice.HasValue) pos.StopLossPrice = order.StopLossPrice;
                }
                else
                {
                    // Closing or reversing
                    var closingQty = Math.Min(pos.Size, order.Quantity);
                    var sideFactor = pos.Side == OrderSide.Buy ? 1 : -1; // long profit when price up
                    var realizedPnl = (executionPrice - pos.EntryPrice) * sideFactor * closingQty;
                    var marginPortion = pos.Margin * (closingQty / pos.Size);

                    balance.AvailableBalance += marginPortion + realizedPnl;
                    balance.Balance += realizedPnl;
                    balance.UsedMargin -= marginPortion;
                    pos.RealizedPnl += realizedPnl;

                    if (order.Quantity < pos.Size)
                    {
                        // Partial close
                        pos.Size -= closingQty;
                        pos.Margin -= marginPortion;
                    }
                    else if (order.Quantity == pos.Size)
                    {
                        // Full close
                        _positions.TryRemove(positionKey, out _);
                        balance.UnrealizedPnl = 0; // all closed
                        balance.UpdatedAt = DateTime.UtcNow;
                        return;
                    }
                    else // reverse
                    {
                        if (order.ReduceOnly)
                        {
                            // ReduceOnly không được reverse, chỉ close toàn bộ position
                            _logger.LogInformation("ReduceOnly order cannot reverse, closing position fully for {U} {Sym}", order.UserId, order.Symbol);
                            _positions.TryRemove(positionKey, out _);
                            balance.UnrealizedPnl = 0;
                            balance.UpdatedAt = DateTime.UtcNow;
                            return;
                        }
                        var extraQty = order.Quantity - pos.Size; // new exposure
                        // Reset to new side
                        pos.Side = order.Side; // opposite of previous
                        pos.Size = extraQty;
                        pos.EntryPrice = executionPrice;
                        pos.Margin = (pos.Size * executionPrice) / Math.Max(1, pos.Leverage);
                        // Allocate new margin
                        balance.AvailableBalance -= pos.Margin; // after releasing old marginPortion earlier
                        balance.UsedMargin += pos.Margin;
                        pos.TakeProfitPrice = order.TakeProfitPrice;
                        pos.StopLossPrice = order.StopLossPrice;
                    }
                }

                // Update mark/unrealized after adjustment
                pos.MarkPrice = executionPrice;
                var sideFactor2 = pos.Side == OrderSide.Buy ? 1 : -1;
                pos.UnrealizedPnl = (executionPrice - pos.EntryPrice) * sideFactor2 * pos.Size;
                pos.PnL = pos.RealizedPnl + pos.UnrealizedPnl;
                // Recompute liquidation price
                pos.LiquidationPrice = ComputeLiquidationPrice(pos.Side, pos.EntryPrice, pos.Leverage, pos.MaintenanceMarginRate);
                pos.UpdatedAt = DateTime.UtcNow;
                _positions[positionKey] = pos;
            }
            else if(!order.ReduceOnly)
            {
                // New position (opening)
                var newPos = new InternalPosition
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = order.UserId,
                    Symbol = order.Symbol,
                    Side = order.Side,
                    Size = order.Quantity,
                    EntryPrice = executionPrice,
                    MarkPrice = executionPrice,
                    Leverage = order.Leverage,
                    Margin = (order.Quantity * executionPrice) / Math.Max(1, order.Leverage),
            TakeProfitPrice = order.TakeProfitPrice,
            StopLossPrice = order.StopLossPrice,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                newPos.LiquidationPrice = ComputeLiquidationPrice(newPos.Side, newPos.EntryPrice, newPos.Leverage, newPos.MaintenanceMarginRate);
                balance.AvailableBalance -= newPos.Margin;
                balance.UsedMargin += newPos.Margin;
                _positions.TryAdd(positionKey, newPos);
            }

            balance.UpdatedAt = DateTime.UtcNow;
        // Recalculate aggregate unrealized PnL across positions for balance
        balance.UnrealizedPnl = _positions.Values.Where(p => p.UserId == order.UserId).Sum(p => p.UnrealizedPnl);
        }

        private decimal? ComputeLiquidationPrice(OrderSide side, decimal entryPrice, int leverage, decimal maintenanceRate)
        {
            if (leverage <= 0 || entryPrice <= 0) return null;
            // Simplified isolated margin formula approximation.
            // Long: entry * (1 - (1/leverage) + maintenanceRate)
            // Short: entry * (1 + (1/leverage) - maintenanceRate)
            if (side == OrderSide.Buy)
            {
                return entryPrice * (1 - (1m / leverage) + maintenanceRate);
            }
            else
            {
                return entryPrice * (1 + (1m / leverage) - maintenanceRate);
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
                        _openConditionalOrders.TryRemove(orderId, out _);
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
            try
            {
                // Lấy từ database để có lịch sử đầy đủ, bao gồm cả orders đã đóng
                var dbOrders = await _db.Orders
                    .Where(o => o.UserWallet == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                // Convert từ Order entity sang InternalOrder
                var internalOrders = dbOrders.Select(o => new InternalOrder
                {
                    Id = o.Id.ToString(),
                    UserId = o.UserWallet,
                    Symbol = o.Symbol,
                    Side = (OrderSide)o.Side,
                    Type = (OrderType)o.Type,
                    Quantity = o.Quantity,
                    Price = o.Price,
                    Status = (OrderStatus)o.Status,
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt ?? o.CreatedAt,
                    FilledQuantity = o.FilledQuantity,
                    AveragePrice = o.AvgPrice, // Sử dụng AvgPrice từ DB entity
                    Leverage = o.Leverage,
                    TakeProfitPrice = o.TakeProfitPrice,
                    StopLossPrice = o.StopLossPrice,
                    ReduceOnly = o.ReduceOnly
                }).ToList();

                // Merge với memory cache (cho các orders mới chưa sync DB)
                lock (_lockObject)
                {
                    var memoryOrders = _orders.Values.Where(o => o.UserId == userId).ToList();
                    foreach (var memOrder in memoryOrders)
                    {
                        if (!internalOrders.Any(io => io.Id == memOrder.Id))
                        {
                            internalOrders.Add(memOrder);
                        }
                    }
                }

                return internalOrders.OrderByDescending(o => o.CreatedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user orders from DB for {UserId}", userId);
                // Fallback to memory cache
                lock (_lockObject)
                {
                    return _orders.Values.Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedAt).ToList();
                }
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
                var list = _positions.Values.Where(p => p.UserId == userId).ToList();
                if (list.Count == 0)
                {
                    // Load from DB if memory empty (server restarted)
                    var dbPositions = _db.Positions.Where(p => p.UserWallet == userId).ToList();
                    foreach (var dp in dbPositions)
                    {
                        var key = $"{userId}:{dp.Symbol}";
                        if (_positions.ContainsKey(key)) continue;
                        var ip = new InternalPosition
                        {
                            Id = dp.Id.ToString(),
                            UserId = userId,
                            Symbol = dp.Symbol,
                            Side = dp.Side,
                            Size = dp.Size,
                            EntryPrice = dp.EntryPrice,
                            MarkPrice = dp.MarkPrice,
                            UnrealizedPnl = dp.UnrealizedPnl,
                            RealizedPnl = dp.RealizedPnl,
                            Leverage = dp.Leverage,
                            Margin = dp.Margin,
                            PnL = dp.PnL,
                            TakeProfitPrice = dp.TakeProfitPrice,
                            StopLossPrice = dp.StopLossPrice,
                            MaintenanceMarginRate = dp.MaintenanceMarginRate,
                            IsIsolated = dp.IsIsolated,
                            LiquidationPrice = dp.LiquidationPrice,
                            CreatedAt = dp.CreatedAt,
                            UpdatedAt = dp.UpdatedAt
                        };
                        _positions.TryAdd(key, ip);
                        list.Add(ip);
                    }
                }
                return list;
            }
        }

        public async Task<InternalUserBalance?> GetUserBalanceAsync(string userId)
        {
            lock (_lockObject)
            {
                if (!_balances.TryGetValue(userId, out var balance))
                {
                    var dbBal = _db.UserBalances.FirstOrDefault(b => b.UserWallet == userId);
                    if (dbBal != null)
                    {
                        balance = new InternalUserBalance
                        {
                            UserId = userId,
                            Balance = dbBal.Balance,
                            AvailableBalance = dbBal.AvailableBalance,
                            UsedMargin = dbBal.MarginUsed,
                            UnrealizedPnl = dbBal.UnrealizedPnl,
                            UpdatedAt = dbBal.UpdatedAt
                        };
                    }
                    else
                    {
                        balance = new InternalUserBalance
                        {
                            UserId = userId,
                            Balance = 0,
                            AvailableBalance = 0,
                            UpdatedAt = DateTime.UtcNow
                        };
                    }
                    _balances[userId] = balance;
                }
                return balance;
            }
        }

        private InternalUserBalance? GetUserBalanceSync(string userId)
        {
            if (_balances.TryGetValue(userId, out var existing)) return existing;
            var dbBal = _db.UserBalances.FirstOrDefault(b => b.UserWallet == userId);
            InternalUserBalance bal;
            if (dbBal != null)
            {
                bal = new InternalUserBalance
                {
                    UserId = userId,
                    Balance = dbBal.Balance,
                    AvailableBalance = dbBal.AvailableBalance,
                    UsedMargin = dbBal.MarginUsed,
                    UnrealizedPnl = dbBal.UnrealizedPnl,
                    UpdatedAt = dbBal.UpdatedAt
                };
            }
            else
            {
                bal = new InternalUserBalance
                {
                    UserId = userId,
                    Balance = 0,
                    AvailableBalance = 0,
                    UpdatedAt = DateTime.UtcNow
                };
            }
            _balances[userId] = bal;
            return bal;
        }

        public async Task<InternalUserBalance> UpdateUserBalanceAsync(string userId, decimal balanceChange, string reason)
        {
            lock (_lockObject)
            {
                if (!_balances.TryGetValue(userId, out var balance))
                {
            var existingDbBal = _db.UserBalances.FirstOrDefault(b => b.UserWallet == userId);
            if (existingDbBal != null)
                    {
                        balance = new InternalUserBalance
                        {
                            UserId = userId,
                Balance = existingDbBal.Balance,
                AvailableBalance = existingDbBal.AvailableBalance,
                UsedMargin = existingDbBal.MarginUsed,
                UnrealizedPnl = existingDbBal.UnrealizedPnl,
                UpdatedAt = existingDbBal.UpdatedAt
                        };
                    }
                    else
                    {
                        balance = new InternalUserBalance
                        {
                            UserId = userId,
                            Balance = 0,
                            AvailableBalance = 0,
                            UpdatedAt = DateTime.UtcNow
                        };
                    }
                }

                balance.Balance += balanceChange;
                balance.AvailableBalance += balanceChange;
                balance.UpdatedAt = DateTime.UtcNow;
                _balances[userId] = balance;

                // Persist to DB (UserBalance + transaction record)
                var dbBal = _db.UserBalances.FirstOrDefault(b => b.UserWallet == userId);
                if (dbBal == null)
                {
                    dbBal = new UserBalance
                    {
                        UserWallet = userId,
                        Balance = balance.Balance,
                        AvailableBalance = balance.AvailableBalance,
                        MarginUsed = balance.UsedMargin,
                        UnrealizedPnl = balance.UnrealizedPnl,
                        UpdatedAt = balance.UpdatedAt
                    };
                    _db.UserBalances.Add(dbBal);
                }
                else
                {
                    dbBal.Balance = balance.Balance;
                    dbBal.AvailableBalance = balance.AvailableBalance;
                    dbBal.MarginUsed = balance.UsedMargin;
                    dbBal.UnrealizedPnl = balance.UnrealizedPnl;
                    dbBal.UpdatedAt = balance.UpdatedAt;
                }

                _db.BalanceTransactions.Add(new BalanceTransaction
                {
                    UserWallet = userId,
                    Amount = balanceChange,
                    Type = reason,
                    BalanceAfter = balance.Balance,
                    Description = reason,
                    CreatedAt = DateTime.UtcNow
                });

                _db.SaveChanges();
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

            if (order.ReduceOnly)
            {
                // Reduce-only: ensure there is an existing position to reduce (same symbol) and clamp quantity if oversized
                var existing = await GetUserPositionAsync(order.UserId, order.Symbol);
                if (existing == null)
                {
                    return (false, "No position to reduce");
                }
                if (order.Quantity > existing.Size)
                {
                    _logger.LogInformation("Clamp reduce-only order qty from {Q} to {Size} for {User} {Sym}", order.Quantity, existing.Size, order.UserId, order.Symbol);
                    order.Quantity = existing.Size; // clamp silently
                }
            }
            else
            {
                // Normal opening/increase order path
                var balance = await GetUserBalanceAsync(order.UserId);
                if (balance == null)
                {
                    return (false, "Insufficient balance");
                }
                if (balance.AvailableBalance <= 0)
                {
                    return (false, "Insufficient balance");
                }
                var requiredMargin = CalculateRequiredMargin(order);
                if (requiredMargin <= 0)
                {
                    return (false, "Invalid order notional");
                }
                if (requiredMargin > balance.AvailableBalance)
                {
                    return (false, $"Insufficient balance. Need {requiredMargin}, available {balance.AvailableBalance}");
                }
            }

            // Check leverage limits (leverage irrelevant for pure reduce-only but we keep uniform validation)
            var maxLeverage = _tradingConfig.MaxLeveragePerSymbol.TryGetValue(order.Symbol, out var leverage) 
                ? leverage 
                : _tradingConfig.MaxLeveragePerSymbol["Default"];
            
            if (order.Leverage > maxLeverage)
            {
                return (false, $"Leverage too high. Maximum for {order.Symbol}: {maxLeverage}x");
            }

            // Defensive price bounds for limit/stop inputs
            if (order.Price.HasValue && (order.Price.Value <= 0 || order.Price.Value > 1_000_000_000m))
                return (false, "Invalid limit price");
            if (order.StopPrice.HasValue && (order.StopPrice.Value <= 0 || order.StopPrice.Value > 1_000_000_000m))
                return (false, "Invalid stop price");

            // Pre-check potential notional to give clearer message (rough) using configurable limit
            try
            {
                var indicative = order.Price ?? order.StopPrice ?? await _priceService.GetMarkPriceAsync(order.Symbol);
                if (indicative <= 0) return (false, "Invalid market price");
                // Apply same symbol-based cap logic as margin calculation to avoid inflated rough notional
                decimal symbolCap = order.Symbol switch
                {
                    var s when s.Contains("BTC") => 2_000_000m,
                    var s when s.Contains("ETH") => 200_000m,
                    var s when s.Contains("BNB") => 20_000m,
                    _ => 1_000_000m
                };
                if (indicative > symbolCap) indicative = symbolCap;
                var roughNotional = indicative * order.Quantity;
                if (roughNotional > _tradingConfig.MaxNotionalPerOrder)
                {
                    return (false, $"Order notional exceeds platform limit ({_tradingConfig.MaxNotionalPerOrder:N0})");
                }
            }
            catch { /* ignore, already handled downstream */ }

            return (true, string.Empty);
        }

        private decimal CalculateRequiredMargin(InternalOrder order)
        {
            // Lấy giá mark thực tế (hoặc limit/stop) & xác thực outlier
            var indicativePrice = order.Price ?? order.StopPrice ?? _priceService.GetMarkPriceAsync(order.Symbol).GetAwaiter().GetResult();
            decimal realisticMax2 = order.Symbol.Contains("BTC") ? 200_000m : order.Symbol.Contains("ETH") ? 10_000m : order.Symbol.Contains("BNB") ? 2_000m : 100_000m;
            decimal realisticMin2 = order.Symbol.Contains("BTC") ? 100m : order.Symbol.Contains("ETH") ? 10m : order.Symbol.Contains("BNB") ? 1m : 0.001m;
            if (indicativePrice <= 0 || indicativePrice > realisticMax2 * 5 || indicativePrice < realisticMin2 / 5)
            {
                _logger.LogWarning("Indicative price outlier {Sym}: {Price}. Using fallback realistic bounds.", order.Symbol, indicativePrice);
                return 0; // buộc ValidateOrder thất bại (Invalid order notional)
            }
            // Guard against quantity overflow
            var qty = order.Quantity;
            if (qty <= 0 || qty > 1_000_000_000m)
            {
                _logger.LogWarning("Quantity abnormal for {Sym}: {Qty}.", order.Symbol, qty);
            }
            var notionalValue = qty * indicativePrice;
            var notionalCap = _tradingConfig.MaxNotionalPerOrder; // configurable cap
            if (notionalValue > notionalCap)
            {
                _logger.LogWarning("Notional too large for {Sym}: {Notional}. Capping to {Cap}.", order.Symbol, notionalValue, notionalCap);
                notionalValue = notionalCap;
            }
            return notionalValue / Math.Max(1, order.Leverage);
        }

        // Temporary fallback price (will be replaced by streaming mark price cache)
    // Removed old static fallback method; rely on IMarketPriceService which already has internal fallback defaults

        // Removed old mock price methods (now using MarketPriceService)

        private void UpsertDbPosition(InternalPosition p)
        {
            var dbPos = _db.Positions.FirstOrDefault(x => x.UserWallet == p.UserId && x.Symbol == p.Symbol);
            if (dbPos == null)
            {
                _db.Positions.Add(new Position
                {
                    UserWallet = p.UserId,
                    Symbol = p.Symbol,
                    Side = p.Side,
                    Size = p.Size,
                    EntryPrice = p.EntryPrice,
                    MarkPrice = p.MarkPrice,
                    UnrealizedPnl = p.UnrealizedPnl,
                    RealizedPnl = p.RealizedPnl,
                    Leverage = p.Leverage,
                    Margin = p.Margin,
                    PnL = p.RealizedPnl + p.UnrealizedPnl,
                    TakeProfitPrice = p.TakeProfitPrice,
                    StopLossPrice = p.StopLossPrice,
                    MaintenanceMarginRate = p.MaintenanceMarginRate,
                    IsIsolated = p.IsIsolated,
                    LiquidationPrice = p.LiquidationPrice,
                    CreatedAt = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                dbPos.Side = p.Side;
                dbPos.Size = p.Size;
                dbPos.EntryPrice = p.EntryPrice;
                dbPos.MarkPrice = p.MarkPrice;
                dbPos.UnrealizedPnl = p.UnrealizedPnl;
                dbPos.RealizedPnl = p.RealizedPnl;
                dbPos.Leverage = p.Leverage;
                dbPos.Margin = p.Margin;
                dbPos.PnL = p.RealizedPnl + p.UnrealizedPnl;
                dbPos.TakeProfitPrice = p.TakeProfitPrice;
                dbPos.StopLossPrice = p.StopLossPrice;
                dbPos.MaintenanceMarginRate = p.MaintenanceMarginRate;
                dbPos.IsIsolated = p.IsIsolated;
                dbPos.LiquidationPrice = p.LiquidationPrice;
                dbPos.UpdatedAt = DateTime.UtcNow;
            }
        }

        public IReadOnlyCollection<InternalPosition> GetAllPositions()
        {
            lock (_lockObject)
            {
                return _positions.Values.ToList();
            }
        }

        // Return snapshot of pending limit/stop orders for external engine processing
        public IReadOnlyCollection<InternalOrder> GetOpenConditionalOrders()
        {
            return _openConditionalOrders.Values.ToList();
        }

        private void IndexConditionalOrder(InternalOrder order)
        {
            if (order.Status == OrderStatus.Pending && order.Type != OrderType.Market)
            {
                _openConditionalOrders[order.Id] = order;
            }
        }

        // Attempt to execute conditional order at current mark price
        public async Task<bool> TryExecuteConditionalAsync(string orderId, decimal markPrice)
        {
            InternalOrder? ord;
            lock (_lockObject)
            {
                if (!_openConditionalOrders.TryGetValue(orderId, out ord)) return false;
                bool fill = false;
                switch (ord.Type)
                {
                    case OrderType.Limit:
                        if (ord.Price.HasValue)
                            fill = (ord.Side == OrderSide.Buy && markPrice <= ord.Price.Value) || (ord.Side == OrderSide.Sell && markPrice >= ord.Price.Value);
                        break;
                    case OrderType.StopMarket:
                        if (ord.StopPrice.HasValue)
                            fill = (ord.Side == OrderSide.Buy && markPrice >= ord.StopPrice.Value) || (ord.Side == OrderSide.Sell && markPrice <= ord.StopPrice.Value);
                        break;
                    case OrderType.StopLimit:
                        if (ord.StopPrice.HasValue && ord.Price.HasValue)
                        {
                            var triggered = (ord.Side == OrderSide.Buy && markPrice >= ord.StopPrice.Value) || (ord.Side == OrderSide.Sell && markPrice <= ord.StopPrice.Value);
                            if (triggered)
                                fill = (ord.Side == OrderSide.Buy && markPrice <= ord.Price.Value) || (ord.Side == OrderSide.Sell && markPrice >= ord.Price.Value);
                        }
                        break;
                }
                if (!fill) return false;
                // convert to market execution
                ord.Type = OrderType.Market;
            }
            // Use limit price if available when converting to market to have accurate entry
            var execPx = ord!.Price ?? ord.StopPrice; // fallback to previously intended trigger price
            // If conditional is reduce-only ensure it doesn't exceed current position size
            if (ord.ReduceOnly)
            {
                var existing = await GetUserPositionAsync(ord.UserId, ord.Symbol);
                if (existing == null) { _openConditionalOrders.TryRemove(orderId, out _); return false; }
                if (ord.Quantity > existing.Size)
                {
                    _logger.LogInformation("Clamp reduce-only conditional exec qty {Q}->{Size} for {U} {Sym}", ord.Quantity, existing.Size, ord.UserId, ord.Symbol);
                    ord.Quantity = existing.Size;
                }
            }
            await ProcessMarketOrderAsync(ord!, execPx);
            _openConditionalOrders.TryRemove(orderId, out _);
            return true;
        }

        public async Task<bool> UpdatePositionRiskAsync(string userId, string symbol, decimal? takeProfit, decimal? stopLoss)
        {
            lock (_lockObject)
            {
                var key = $"{userId}:{symbol}";
                if (!_positions.TryGetValue(key, out var pos))
                {
                    // Thử load từ database (server restart hoặc cache miss)
                    var dbPos = _db.Positions.FirstOrDefault(p => p.UserWallet == userId && p.Symbol == symbol);
                    if (dbPos == null)
                    {
                        return false; // thật sự không có vị thế
                    }
                    pos = new InternalPosition
                    {
                        Id = dbPos.Id.ToString(),
                        UserId = userId,
                        Symbol = dbPos.Symbol,
                        Side = dbPos.Side,
                        Size = dbPos.Size,
                        EntryPrice = dbPos.EntryPrice,
                        MarkPrice = dbPos.MarkPrice,
                        UnrealizedPnl = dbPos.UnrealizedPnl,
                        RealizedPnl = dbPos.RealizedPnl,
                        Leverage = dbPos.Leverage,
                        Margin = dbPos.Margin,
                        PnL = dbPos.PnL,
                        TakeProfitPrice = dbPos.TakeProfitPrice,
                        StopLossPrice = dbPos.StopLossPrice,
                        MaintenanceMarginRate = dbPos.MaintenanceMarginRate,
                        IsIsolated = dbPos.IsIsolated,
                        LiquidationPrice = dbPos.LiquidationPrice,
                        CreatedAt = dbPos.CreatedAt,
                        UpdatedAt = dbPos.UpdatedAt
                    };
                    _positions[key] = pos; // cache lại để các lần sau nhanh hơn
                }

                // Basic validation: TP/SL should be positive (removed strict logic validation)
                if (takeProfit.HasValue && takeProfit <= 0) return false;
                if (stopLoss.HasValue && stopLoss <= 0) return false;

                if (takeProfit.HasValue)
                    pos.TakeProfitPrice = takeProfit;
                if (stopLoss.HasValue)
                    pos.StopLossPrice = stopLoss;
                // Cho phép clear TP/SL nếu gửi null values
                if (!takeProfit.HasValue && !stopLoss.HasValue)
                {
                    // If both explicitly null (client sent nulls) keep existing; nothing to update
                    // But still save to trigger DB update
                }

                pos.UpdatedAt = DateTime.UtcNow;
                _positions[key] = pos;
                UpsertDbPosition(pos);
            }
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<(bool ok, string? error)> ClosePositionAsync(string userId, string symbol)
        {
            symbol = NormalizeSymbol(symbol);
            InternalPosition? pos;
            lock (_lockObject)
            {
                var key = $"{userId}:{symbol}";
                _positions.TryGetValue(key, out pos);
            }
            if (pos == null)
            {
                // Try DB load
                var dbPos = _db.Positions.FirstOrDefault(p => p.UserWallet == userId && p.Symbol == symbol);
                if (dbPos == null) return (false, "No open position");
                pos = new InternalPosition
                {
                    Id = dbPos.Id.ToString(),
                    UserId = userId,
                    Symbol = dbPos.Symbol,
                    Side = dbPos.Side,
                    Size = dbPos.Size,
                    EntryPrice = dbPos.EntryPrice,
                    MarkPrice = dbPos.MarkPrice,
                    UnrealizedPnl = dbPos.UnrealizedPnl,
                    RealizedPnl = dbPos.RealizedPnl,
                    Leverage = dbPos.Leverage,
                    Margin = dbPos.Margin,
                    PnL = dbPos.PnL,
                    TakeProfitPrice = dbPos.TakeProfitPrice,
                    StopLossPrice = dbPos.StopLossPrice,
                    MaintenanceMarginRate = dbPos.MaintenanceMarginRate,
                    IsIsolated = dbPos.IsIsolated,
                    LiquidationPrice = dbPos.LiquidationPrice,
                    CreatedAt = dbPos.CreatedAt,
                    UpdatedAt = dbPos.UpdatedAt
                };
                lock (_lockObject) { _positions[$"{userId}:{symbol}"] = pos; }
            }

            var closeOrder = new InternalOrder
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Symbol = symbol,
                Side = pos.Side, // Same side as position for reduce-only order to close it
                Type = OrderType.Market,
                Quantity = pos.Size,
                Leverage = pos.Leverage,
                ReduceOnly = true,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            var result = await CreateOrderAsync(closeOrder);
            if (result.Status == OrderStatus.Rejected)
            {
                return (false, result.Notes ?? "Rejected");
            }
            return (true, null);
        }
    }
}