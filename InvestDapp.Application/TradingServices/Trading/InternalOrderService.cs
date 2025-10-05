using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Models.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using InvestDapp.Infrastructure.Data.interfaces;
using Microsoft.EntityFrameworkCore;

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
        Task<bool> UpdatePositionRiskAsync(string userId, string symbol, decimal? takeProfit, decimal? stopLoss, string? positionId = null);
        Task<(bool ok, string? error)> ClosePositionAsync(string userId, string symbol, string? positionId = null);
    }

    public class InternalOrderService : IInternalOrderService
    {
        private readonly ConcurrentDictionary<string, InternalOrder> _orders;
        private readonly ConcurrentDictionary<string, InternalPosition> _positions;
        private readonly ConcurrentDictionary<string, InternalUserBalance> _balances;
        // Track pending conditional (non-market) orders
        private readonly ConcurrentDictionary<string, InternalOrder> _openConditionalOrders = new();
        private readonly TradingConfig _tradingConfig;
        private readonly ITradingRepository _repo;
        private readonly IMarketPriceService _priceService;
        private readonly ILogger<InternalOrderService> _logger;
        private readonly InvestDbContext _dbContext;
        private readonly object _lockObject = new();

        public InternalOrderService(IOptions<TradingConfig> tradingConfig,
            ILogger<InternalOrderService> logger,
            ITradingRepository repo,
            IMarketPriceService priceService,
            InvestDbContext dbContext)
        {
            _orders = new ConcurrentDictionary<string, InternalOrder>();
            _positions = new ConcurrentDictionary<string, InternalPosition>();
            _balances = new ConcurrentDictionary<string, InternalUserBalance>();
            _tradingConfig = tradingConfig.Value;
            _logger = logger;
            _repo = repo;
            _priceService = priceService;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Lấy cấu hình phí active từ database
        /// </summary>
        private TradingFeeConfig? GetActiveFeeConfig()
        {
            try
            {
                return _dbContext.TradingFeeConfigs
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active fee config from database");
                // Return default config if database fails
                return new TradingFeeConfig
                {
                    MakerFeePercent = 0.02,
                    TakerFeePercent = 0.04,
                    WithdrawalFeePercent = 0.5
                };
            }
        }

        public async Task<InternalOrder> CreateOrderAsync(InternalOrder order)
        {
            // Normalize symbol
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
            order.CreatedAt = DateTime.UtcNow;

            _orders.TryAdd(order.Id, order);

            // Persist a minimal DB order record so history exists immediately
            var dbOrder = new Order
            {
                InternalOrderId = order.Id,
                UserWallet = order.UserId,
                Symbol = order.Symbol,
                Side = order.Side,
                Type = order.Type,
                Quantity = order.Quantity,
                Price = order.Price,
                StopPrice = order.StopPrice,
                ReduceOnly = order.ReduceOnly,
                Leverage = order.Leverage,
                Status = order.Status,
                CreatedAt = order.CreatedAt
            };
            await _repo.AddOrderAsync(dbOrder);
            await _repo.SaveChangesAsync();

            // If market order -> execute immediately
            if (order.Type == OrderType.Market)
            {
                decimal? execPx = null;
                try
                {
                    execPx = order.Price ?? order.StopPrice ?? await _priceService.GetMarkPriceAsync(order.Symbol);
                }
                catch { execPx = null; }

                await ProcessMarketOrderAsync(order, execPx);
            }
            else
            {
                IndexConditionalOrder(order);
            }

            return order;
        }

        private async Task ProcessMarketOrderAsync(InternalOrder order, decimal? providedPrice = null)
        {
            // Determine execution price
            decimal executionPrice = providedPrice ?? await _priceService.GetMarkPriceAsync(order.Symbol);

            // Basic realistic bounds to avoid absurd fills
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

            if (executionPrice <= 0 || executionPrice > realisticMax * 5 || executionPrice < realisticMin / 5)
            {
                order.Status = OrderStatus.Rejected;
                order.Notes = "Giá thị trường bất thường. Thử lại sau";
                _logger.LogWarning("Reject order {Id} do giá bất thường {Price} {Sym}", order.Id, executionPrice, order.Symbol);
                return;
            }

            if (order.Price.HasValue && (order.Price <= 0 || order.Price > 1_000_000_000m)) order.Price = null;
            if (order.StopPrice.HasValue && (order.StopPrice <= 0 || order.StopPrice > 1_000_000_000m)) order.StopPrice = null;

            lock (_lockObject)
            {
                if (!_orders.TryGetValue(order.Id, out var currentOrder)) return;
                currentOrder.Status = OrderStatus.Filled;
                currentOrder.FilledQuantity = currentOrder.Quantity;
                executionPrice = Math.Round(executionPrice, 2, MidpointRounding.AwayFromZero);
                currentOrder.AveragePrice = executionPrice;
                currentOrder.UpdatedAt = DateTime.UtcNow;
                _orders[order.Id] = currentOrder;

                var dbOrder = _repo.GetOrderByInternalIdAsync(currentOrder.Id).GetAwaiter().GetResult();
                if (dbOrder != null)
                {
                    dbOrder.Status = OrderStatus.Filled;
                    dbOrder.FilledQuantity = currentOrder.FilledQuantity;
                    dbOrder.AvgPrice = currentOrder.AveragePrice;
                    dbOrder.UpdatedAt = DateTime.UtcNow;
                }

                // Update in-memory positions and balances
                UpdateUserPosition(currentOrder, executionPrice);

                // Lấy fee config từ database
                var feeConfig = GetActiveFeeConfig();
                
                // Xác định loại phí: Maker (limit order) hoặc Taker (market order)
                // Market orders và orders execute ngay là Taker
                // Limit orders chờ match là Maker
                var isTaker = currentOrder.Type == OrderType.Market || currentOrder.Type == OrderType.StopMarket;
                var feePercent = isTaker ? 
                    (feeConfig?.TakerFeePercent ?? 0.04) : 
                    (feeConfig?.MakerFeePercent ?? 0.02);
                
                var tradingFeeRate = (decimal)feePercent / 100m; // Convert percent to decimal (0.04% -> 0.0004)
                var tradingFee = (currentOrder.Quantity * executionPrice) * tradingFeeRate;
                
                _logger.LogInformation("Trading fee calculated: {FeePercent}% ({FeeType}) for order {OrderId} ({OrderType}), amount: {Fee} BNB", 
                    feePercent, isTaker ? "Taker" : "Maker", currentOrder.Id, currentOrder.Type, tradingFee);
                
                var balance = GetUserBalanceSync(currentOrder.UserId);
                if (balance != null)
                {
                    balance.Balance -= tradingFee;
                    balance.AvailableBalance -= tradingFee;
                    balance.UpdatedAt = DateTime.UtcNow;
                    _balances[currentOrder.UserId] = balance;
                    
                    _logger.LogInformation("Trading fee deducted: {Fee} for order {OrderId}, user {UserId}", 
                        tradingFee, currentOrder.Id, currentOrder.UserId);

                    var toPersist = new UserBalance
                    {
                        UserWallet = currentOrder.UserId,
                        Balance = balance.Balance,
                        AvailableBalance = balance.AvailableBalance,
                        MarginUsed = balance.UsedMargin,
                        UnrealizedPnl = balance.UnrealizedPnl,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _repo.AddOrUpdateUserBalanceAsync(toPersist).GetAwaiter().GetResult();
                    
                    // Tạo transaction record cho phí giao dịch
                    _repo.AddBalanceTransactionAsync(new BalanceTransaction
                    {
                        UserWallet = currentOrder.UserId,
                        Amount = -tradingFee,
                        Type = "TRADING_FEE",
                        Reference = currentOrder.Id,
                        Description = $"Trading fee for {currentOrder.Side} {currentOrder.Quantity} {currentOrder.Symbol}",
                        BalanceAfter = balance.Balance,
                        CreatedAt = DateTime.UtcNow
                    }).GetAwaiter().GetResult();
                }
                    var memPositions = _positions.Values.Where(p => p.UserId == currentOrder.UserId && p.Symbol == currentOrder.Symbol).ToList();
                    if (memPositions.Count > 0)
                    {
                        foreach (var m in memPositions)
                        {
                            UpsertDbPosition(m);
                        }
                    }
            }

            await _repo.SaveChangesAsync();
            _logger.LogInformation("Market order executed: {OrderId} at price {Price}", order.Id, executionPrice);
        }

        private void UpdateUserPosition(InternalOrder order, decimal executionPrice)
        {
            var balance = GetUserBalanceSync(order.UserId)!; // ensured non-null

            // Collect existing positions for this user+symbol
            var existingPositions = _positions.Values.Where(p => p.UserId == order.UserId && p.Symbol == order.Symbol).OrderBy(p => p.CreatedAt).ToList();

            // If reduce-only and no in-memory positions, sync from DB so we can close DB rows correctly
            if (order.ReduceOnly && existingPositions.Count == 0)
            {
                try
                {
                    var dbPositions = _repo.GetPositionsByUserSymbolAsync(order.UserId, order.Symbol).GetAwaiter().GetResult();
                    foreach (var dp in dbPositions)
                    {
                        var ip = new InternalPosition
                        {
                            Id = dp.Id.ToString(),
                            UserId = dp.UserWallet,
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
                        var k = $"{order.UserId}:{dp.Symbol}:{dp.Id}";
                        _positions[k] = ip;
                    }
                    existingPositions = _positions.Values.Where(p => p.UserId == order.UserId && p.Symbol == order.Symbol).OrderBy(p => p.CreatedAt).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing DB positions into memory for reduce-only close for {User} {Sym}", order.UserId, order.Symbol);
                }
            }

            if (order.ReduceOnly)
            {
                // Closing behavior: consume existing positions FIFO until order.Quantity satisfied
                var remaining = order.Quantity;
                var positionsToRemove = new List<InternalPosition>();

                // If the order targets a specific DB position, prioritize it first
                List<InternalPosition> orderedPositions = existingPositions;
                if (!string.IsNullOrEmpty(order.TargetPositionId))
                {
                    orderedPositions = existingPositions.OrderByDescending(p => p.Id == order.TargetPositionId ? 1 : 0).ThenBy(p => p.CreatedAt).ToList();
                }

                foreach (var pos in orderedPositions)
                {
                    if (remaining <= 0) break;
                    var closingQty = Math.Min(pos.Size, remaining);
                    var sideFactor = pos.Side == OrderSide.Buy ? 1 : -1;
                    var realizedPnl = (executionPrice - pos.EntryPrice) * sideFactor * closingQty;
                    var marginPortion = pos.Margin * (closingQty / pos.Size);

                    balance.AvailableBalance += marginPortion + realizedPnl;
                    balance.Balance += realizedPnl;
                    balance.UsedMargin -= marginPortion;
                    pos.RealizedPnl += realizedPnl;

                    if (closingQty >= pos.Size - 0.0000001m)
                    {
                        // fully close this position
                        positionsToRemove.Add(pos);
                    }
                    else
                    {
                        // partially close
                        pos.Size -= closingQty;
                        pos.Margin -= marginPortion;
                        pos.UnrealizedPnl = (executionPrice - pos.EntryPrice) * (pos.Side == OrderSide.Buy ? 1 : -1) * pos.Size;
                        pos.PnL = pos.RealizedPnl + pos.UnrealizedPnl;
                        pos.UpdatedAt = DateTime.UtcNow;
                        // persist partial change
                        UpsertDbPosition(pos);
                    }

                    remaining -= closingQty;
                }

                // Remove fully closed positions from memory and DB
                var removedAny = false;
                foreach (var closedPos in positionsToRemove)
                {
                    // remove from in-memory cache
                    var key = _positions.Keys.FirstOrDefault(k => k.EndsWith(":" + closedPos.Id));
                    if (!string.IsNullOrEmpty(key)) _positions.TryRemove(key, out _);

                    // attempt to remove DB row if exists
                    try
                    {
                        if (int.TryParse(closedPos.Id, out var dbId))
                        {
                            var dbPos = _repo.GetPositionByIdAsync(dbId).GetAwaiter().GetResult();
                            if (dbPos != null) { _repo.RemovePositionAsync(dbPos).GetAwaiter().GetResult(); removedAny = true; }
                        }
                        else
                        {
                            // best-effort: find a matching DB row for this user+symbol+side+entryprice
                            // Narrow fallback match to include Size and Leverage to avoid removing unrelated rows with same entry price
                            var dbList = _repo.GetPositionsByUserSymbolAsync(closedPos.UserId, closedPos.Symbol).GetAwaiter().GetResult();
                            var dbPos = dbList.Where(p => p.UserWallet == closedPos.UserId
                                                                  && p.Symbol == closedPos.Symbol
                                                                  && p.Side == closedPos.Side
                                                                  && p.EntryPrice == closedPos.EntryPrice
                                                                  && p.Size == closedPos.Size
                                                                  && p.Leverage == closedPos.Leverage)
                                .OrderBy(p => p.CreatedAt).FirstOrDefault();
                            if (dbPos != null) { _repo.RemovePositionAsync(dbPos).GetAwaiter().GetResult(); removedAny = true; }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error removing closed position for {User} {Sym}", closedPos.UserId, closedPos.Symbol);
                    }
                }

                // Persist DB removals immediately so consumers see positions disappear
                if (removedAny)
                {
                    try
                    {
                        _repo.SaveChangesAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving DB changes after removing closed positions for {User} {Sym}", order.UserId, order.Symbol);
                    }
                }

                // (DB rows for fully closed positions are removed above per-closedPos.)

                // If user tried to reduce more than total open, clamp
                var totalOpen = existingPositions.Sum(p => p.Size);
                if (order.Quantity > totalOpen)
                {
                    _logger.LogInformation("Reduce-only quantity {Q} exceeds total open {T}, clamped for {U} {Sym}", order.Quantity, totalOpen, order.UserId, order.Symbol);
                }
            }
            else
            {
                // Always create a new separate position for non-reduce (opening) orders
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

                var key = $"{order.UserId}:{order.Symbol}:{newPos.Id}";
                _positions.TryAdd(key, newPos);
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

        private string NormalizeSymbol(string? symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return string.Empty;
            var s = symbol.ToUpperInvariant().Trim();
            if (s.EndsWith("USDT") || s.EndsWith("USD")) return s;
            return s + "USDT";
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
                var dbOrders = await _repo.GetOrdersByUserAsync(userId);

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
            // Always prefer DB as source-of-truth. Load latest positions for this user+symbol and return the first.
            symbol = NormalizeSymbol(symbol);
            var dpList = await _repo.GetPositionsByUserSymbolAsync(userId, symbol);
            var dp = dpList.OrderByDescending(p => p.CreatedAt).FirstOrDefault();

            if (dp == null) return null;

            var ip = new InternalPosition
            {
                Id = dp.Id.ToString(),
                UserId = dp.UserWallet,
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

            // Ensure cache reflects DB
            var key = $"{userId}:{dp.Symbol}:{dp.Id}";
            _positions[key] = ip;
            return ip;
        }

        public async Task<List<InternalPosition>> GetUserPositionsAsync(string userId)
        {
            // DB is source-of-truth. Load all positions for the user from DB and sync in-memory cache.
            var dbPositions = await _repo.GetPositionsByUserAsync(userId);
            var result = new List<InternalPosition>(dbPositions.Count);

            // Remove any in-memory positions for this user to avoid stale entries, then repopulate from DB
            var keysToRemove = _positions.Keys.Where(k => k.StartsWith(userId + ":")).ToList();
            foreach (var k in keysToRemove) _positions.TryRemove(k, out _);

            foreach (var dp in dbPositions)
            {
                var ip = new InternalPosition
                {
                    Id = dp.Id.ToString(),
                    UserId = dp.UserWallet,
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
                var key = $"{userId}:{dp.Symbol}:{dp.Id}";
                _positions[key] = ip;
                result.Add(ip);
            }

            return result;
        }

        public async Task<InternalUserBalance?> GetUserBalanceAsync(string userId)
        {
            // Always read latest from DB and update memory cache
            var dbBal = await _repo.GetUserBalanceAsync(userId);
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

        private InternalUserBalance? GetUserBalanceSync(string userId)
        {
            // Sync from DB synchronously (used from within locks)
            if (_balances.TryGetValue(userId, out var existing)) return existing;
            var dbBal = _repo.GetUserBalanceAsync(userId).GetAwaiter().GetResult();
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
                    var existingDbBal = _repo.GetUserBalanceAsync(userId).GetAwaiter().GetResult();
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

                // Persist to DB (UserBalance + transaction record) via repository
                var toPersist = new UserBalance
                {
                    UserWallet = userId,
                    Balance = balance.Balance,
                    AvailableBalance = balance.AvailableBalance,
                    MarginUsed = balance.UsedMargin,
                    UnrealizedPnl = balance.UnrealizedPnl,
                    UpdatedAt = balance.UpdatedAt
                };
                _repo.AddOrUpdateUserBalanceAsync(toPersist).GetAwaiter().GetResult();
                _repo.AddBalanceTransactionAsync(new BalanceTransaction
                {
                    UserWallet = userId,
                    Amount = balanceChange,
                    Type = reason,
                    BalanceAfter = balance.Balance,
                    Description = reason,
                    CreatedAt = DateTime.UtcNow
                }).GetAwaiter().GetResult();

                _repo.SaveChangesAsync().GetAwaiter().GetResult();
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
                // Reduce-only: ensure there are positions in DB for this user+symbol and clamp to total open size
                var dbPositions = await _repo.GetPositionsByUserSymbolAsync(order.UserId, order.Symbol);
                var totalOpen = dbPositions.Sum(p => p.Size);
                if (totalOpen <= 0)
                {
                    // also check in-memory cache as fallback
                    var mem = _positions.Values.Where(p => p.UserId == order.UserId && p.Symbol == order.Symbol).ToList();
                    totalOpen = mem.Sum(p => p.Size);
                }
                if (totalOpen <= 0) return (false, "No position to reduce");
                if (order.Quantity > totalOpen)
                {
                    _logger.LogInformation("Clamp reduce-only order qty from {Q} to {Size} for {User} {Sym}", order.Quantity, totalOpen, order.UserId, order.Symbol);
                    order.Quantity = totalOpen; // clamp silently
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
            // If Id parses to DB id -> update via repo
            if (int.TryParse(p.Id, out var dbId))
            {
                var dbPos = _repo.GetPositionByIdAsync(dbId).GetAwaiter().GetResult();
                if (dbPos != null)
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
                    _repo.UpsertPositionAsync(dbPos).GetAwaiter().GetResult();
                }
                return;
            }

            // Insert a new DB row (allow multiple DB positions per symbol) and map generated id back
            var newDbPos = new Position
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
            };
            try
            {
                var persisted = _repo.UpsertPositionAsync(newDbPos).GetAwaiter().GetResult();
                var oldId = p.Id;
                p.Id = persisted.Id.ToString();

                try
                {
                    var existingKey = _positions.Keys.FirstOrDefault(k =>
                        _positions.TryGetValue(k, out var val) && ReferenceEquals(val, p));
                    if (!string.IsNullOrEmpty(existingKey))
                    {
                        var expectedSuffix = ":" + oldId;
                        if (existingKey.EndsWith(expectedSuffix))
                        {
                            _positions.TryRemove(existingKey, out _);
                            var newKey = $"{p.UserId}:{p.Symbol}:{p.Id}";
                            _positions[newKey] = p;
                        }
                    }
                }
                catch (Exception ex2)
                {
                    _logger.LogWarning(ex2, "Failed to remap in-memory position key after DB insert for {User} {Sym}", p.UserId, p.Symbol);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist new position for user {User} {Sym}", p.UserId, p.Symbol);
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
                var existingList = (await GetUserPositionsAsync(ord.UserId)).Where(p => p.Symbol == ord.Symbol).ToList();
                if (existingList == null || existingList.Count == 0) { _openConditionalOrders.TryRemove(orderId, out _); return false; }
                var totalOpen = existingList.Sum(p => p.Size);
                if (ord.Quantity > totalOpen)
                {
                    _logger.LogInformation("Clamp reduce-only conditional exec qty {Q}->{Size} for {U} {Sym}", ord.Quantity, totalOpen, ord.UserId, ord.Symbol);
                    ord.Quantity = totalOpen;
                }
            }
            await ProcessMarketOrderAsync(ord!, execPx);
            _openConditionalOrders.TryRemove(orderId, out _);
            return true;
        }

        public async Task<bool> UpdatePositionRiskAsync(string userId, string symbol, decimal? takeProfit, decimal? stopLoss, string? positionId = null)
        {
            symbol = NormalizeSymbol(symbol);

            // Validate basic values first
            if (takeProfit.HasValue && takeProfit <= 0) return false;
            if (stopLoss.HasValue && stopLoss <= 0) return false;

            lock (_lockObject)
            {
                // If a specific positionId provided, update only that DB row
                if (!string.IsNullOrEmpty(positionId))
                {
                    if (!int.TryParse(positionId, out var pid)) return false;
                    var dbPos = _repo.GetPositionByIdAsync(pid).GetAwaiter().GetResult();
                    if (dbPos == null || dbPos.UserWallet != userId || dbPos.Symbol != symbol) return false;

                    if (takeProfit.HasValue) dbPos.TakeProfitPrice = takeProfit;
                    else dbPos.TakeProfitPrice = null;

                    if (stopLoss.HasValue) dbPos.StopLossPrice = stopLoss;
                    else dbPos.StopLossPrice = null;

                    dbPos.UpdatedAt = DateTime.UtcNow;

                    // Update in-memory cache entry if exists, or create one
                    var key = $"{userId}:{dbPos.Symbol}:{dbPos.Id}";
                    if (_positions.TryGetValue(key, out var cached))
                    {
                        cached.TakeProfitPrice = dbPos.TakeProfitPrice;
                        cached.StopLossPrice = dbPos.StopLossPrice;
                        cached.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        var ip = new InternalPosition
                        {
                            Id = dbPos.Id.ToString(),
                            UserId = dbPos.UserWallet,
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
                        _positions[key] = ip;
                    }

                    // Persist change
                    _repo.UpsertPositionAsync(dbPos).GetAwaiter().GetResult();
                    _repo.SaveChangesAsync().GetAwaiter().GetResult();
                    return true;
                }

                // No positionId: update all positions for this user+symbol
                var matches = _positions.Values.Where(p => p.UserId == userId && p.Symbol == symbol).ToList();
                if (matches.Count == 0)
                {
                    var dbPositions = _repo.GetPositionsByUserSymbolAsync(userId, symbol).GetAwaiter().GetResult();
                    foreach (var dp in dbPositions)
                    {
                        var k = $"{userId}:{dp.Symbol}:{dp.Id}";
                        if (_positions.ContainsKey(k)) continue;
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
                        _positions[k] = ip;
                        matches.Add(ip);
                    }
                }

                if (matches.Count == 0) return false;

                foreach (var pos in matches)
                {
                    if (takeProfit.HasValue) pos.TakeProfitPrice = takeProfit;
                    else pos.TakeProfitPrice = null;

                    if (stopLoss.HasValue) pos.StopLossPrice = stopLoss;
                    else pos.StopLossPrice = null;

                    pos.UpdatedAt = DateTime.UtcNow;
                    UpsertDbPosition(pos);
                }
            }

            await _repo.SaveChangesAsync();
            return true;
        }

        public async Task<(bool ok, string? error)> ClosePositionAsync(string userId, string symbol, string? positionId = null)
        {
            symbol = NormalizeSymbol(symbol);
            // If a specific positionId supplied, close only that position
            if (!string.IsNullOrEmpty(positionId))
            {
                if (!int.TryParse(positionId, out var pid)) return (false, "Invalid positionId");
                var dbPosSingle = _repo.GetPositionByIdAsync(pid).GetAwaiter().GetResult();
                if (dbPosSingle == null || dbPosSingle.UserWallet != userId || dbPosSingle.Symbol != symbol) return (false, "Position not found");

                var closeOrderSingle = new InternalOrder
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Symbol = symbol,
                    Side = dbPosSingle.Side,
                    Type = OrderType.Market,
                    Quantity = dbPosSingle.Size,
                    Leverage = dbPosSingle.Leverage,
                    ReduceOnly = true,
                    TargetPositionId = dbPosSingle.Id.ToString(),
                    Status = OrderStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                var resSingle = await CreateOrderAsync(closeOrderSingle);
                if (resSingle.Status == OrderStatus.Rejected) return (false, resSingle.Notes ?? "Rejected");
                return (true, null);
            }

            // Load DB positions for this symbol (most recent first)
            var positions = _repo.GetPositionsByUserSymbolAsync(userId, symbol).GetAwaiter().GetResult().OrderByDescending(p => p.CreatedAt).ToList();

            if (positions.Count == 0)
            {
                // Check in-memory cache too and close the most recent in-memory position only
                InternalOrder? preparedOrder = null;
                lock (_lockObject)
                {
                    var mem = _positions.Values.Where(p => p.UserId == userId && p.Symbol == symbol).OrderByDescending(p => p.CreatedAt).ToList();
                    if (mem.Count == 0) return (false, "No open position");
                    var toClose = mem.First();
                    preparedOrder = new InternalOrder
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        Symbol = symbol,
                        Side = toClose.Side,
                        Type = OrderType.Market,
                        Quantity = toClose.Size,
                        Leverage = toClose.Leverage,
                        ReduceOnly = true,
                        Status = OrderStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };
                }
                var resultMem = await CreateOrderAsync(preparedOrder!);
                if (resultMem.Status == OrderStatus.Rejected) return (false, resultMem.Notes ?? "Rejected");
                return (true, null);
            }

            // Close the most recent DB position only (unless positionId was provided earlier)
            var targetDbPos = positions.First();
            var closeOrder = new InternalOrder
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Symbol = symbol,
                Side = targetDbPos.Side, // Use side of the target position
                Type = OrderType.Market,
                Quantity = targetDbPos.Size,
                Leverage = targetDbPos.Leverage,
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