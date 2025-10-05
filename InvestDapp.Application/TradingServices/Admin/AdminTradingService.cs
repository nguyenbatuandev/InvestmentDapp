using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.DTOs.Admin;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvestDapp.Application.TradingServices.Admin
{
    public class AdminTradingService : IAdminTradingService
    {
        private readonly InvestDbContext _context;
        private readonly ILogger<AdminTradingService> _logger;

        public AdminTradingService(InvestDbContext context, ILogger<AdminTradingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<TradingDashboardDto> GetDashboardAsync()
        {
            try
            {
                var stats = new TradingStatsDto
                {
                    TotalUsers = await _context.UserBalances.CountAsync(),
                    ActiveUsers = await _context.UserBalances.CountAsync(u => u.Balance > 0),
                    TotalBalance = await _context.UserBalances.SumAsync(u => (decimal?)u.Balance) ?? 0,
                    TotalMarginUsed = await _context.UserBalances.SumAsync(u => (decimal?)u.MarginUsed) ?? 0,
                    TotalOrders = await _context.Orders.CountAsync(),
                    OpenPositions = await _context.Positions.CountAsync(),
                    TotalPnL = await _context.Positions.SumAsync(p => (decimal?)p.RealizedPnl) ?? 0,
                    TotalFees = await _context.BalanceTransactions
                        .Where(t => (t.Type == "TRADING_FEE" || t.Type == "WITHDRAWAL_FEE") && t.Amount < 0)
                        .SumAsync(t => (decimal?)(-t.Amount)) ?? 0,
                    PendingWithdrawals = await _context.WalletWithdrawalRequests
                        .CountAsync(w => w.Status == WithdrawalStatus.Pending),
                    PendingWithdrawalAmount = await _context.WalletWithdrawalRequests
                        .Where(w => w.Status == WithdrawalStatus.Pending)
                        .SumAsync(w => (decimal?)w.Amount) ?? 0
                };

                var topTraders = await GetTopTradersInternalAsync(10);
                var recentActivities = await GetRecentActivitiesAsync(20);
                var pendingWithdrawals = await GetPendingWithdrawalsAsync();
                var feeConfig = await GetActiveFeeConfigAsync();

                return new TradingDashboardDto
                {
                    Stats = stats,
                    TopTraders = topTraders,
                    RecentActivities = recentActivities,
                    PendingWithdrawals = pendingWithdrawals,
                    CurrentFeeConfig = feeConfig
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trading dashboard");
                throw;
            }
        }

        public async Task<List<TopTraderDto>> GetAllTradersAsync(int page = 1, int pageSize = 50)
        {
            return await GetTopTradersInternalAsync(pageSize, (page - 1) * pageSize);
        }

        private async Task<List<TopTraderDto>> GetTopTradersInternalAsync(int take, int skip = 0)
        {
            var traders = await _context.UserBalances
                .OrderByDescending(u => u.Balance)
                .Skip(skip)
                .Take(take)
                .Select(u => new
                {
                    u.UserWallet,
                    u.Balance,
                    u.UpdatedAt
                })
                .ToListAsync();

            var result = new List<TopTraderDto>();

            foreach (var trader in traders)
            {
                var totalOrders = await _context.Orders.CountAsync(o => o.UserWallet == trader.UserWallet);
                var openPositions = await _context.Positions.CountAsync(p => p.UserWallet == trader.UserWallet);
                var totalPnL = await _context.Positions
                    .Where(p => p.UserWallet == trader.UserWallet)
                    .SumAsync(p => (decimal?)p.RealizedPnl) ?? 0;

                var filledOrders = await _context.Orders
                    .Where(o => o.UserWallet == trader.UserWallet && o.Status == OrderStatus.Filled)
                    .CountAsync();

                var winRate = totalOrders > 0 ? (decimal)filledOrders / totalOrders * 100 : 0;

                result.Add(new TopTraderDto
                {
                    UserWallet = trader.UserWallet,
                    Balance = trader.Balance,
                    TotalPnL = totalPnL,
                    TotalOrders = totalOrders,
                    OpenPositions = openPositions,
                    WinRate = winRate,
                    LastActive = trader.UpdatedAt
                });
            }

            return result;
        }

        private async Task<List<TradingActivityDto>> GetRecentActivitiesAsync(int count)
        {
            var activities = new List<TradingActivityDto>();

            // Recent orders
            var orders = await _context.Orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(count / 2)
                .Select(o => new TradingActivityDto
                {
                    Type = "ORDER",
                    UserWallet = o.UserWallet,
                    Symbol = o.Symbol,
                    Amount = o.Quantity,
                    Status = o.Status.ToString(),
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            // Recent transactions
            var transactions = await _context.BalanceTransactions
                .OrderByDescending(t => t.CreatedAt)
                .Take(count / 2)
                .Select(t => new TradingActivityDto
                {
                    Type = t.Type,
                    UserWallet = t.UserWallet,
                    Amount = t.Amount,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            activities.AddRange(orders);
            activities.AddRange(transactions);

            return activities.OrderByDescending(a => a.CreatedAt).Take(count).ToList();
        }

        public async Task<UserTradingDetailDto?> GetUserDetailAsync(string userWallet)
        {
            try
            {
                var balance = await _context.UserBalances
                    .FirstOrDefaultAsync(b => b.UserWallet == userWallet);

                if (balance == null)
                    return null;

                // Get all trading fees for this user to match with orders
                var tradingFees = await _context.BalanceTransactions
                    .Where(t => t.UserWallet == userWallet && t.Type == "TRADING_FEE")
                    .Select(t => new { t.Reference, Fee = -t.Amount }) // Fee is negative in transactions
                    .ToListAsync();
                
                // Create dictionary with order ID as key (Reference is stored as orderId string)
                var feeDict = new Dictionary<string, decimal>();
                foreach (var fee in tradingFees)
                {
                    if (!string.IsNullOrEmpty(fee.Reference))
                    {
                        feeDict[fee.Reference] = fee.Fee;
                    }
                }

                var orders = await _context.Orders
                    .Where(o => o.UserWallet == userWallet && o.Status == OrderStatus.Filled)
                    .OrderBy(o => o.CreatedAt)
                    .ToListAsync();

                // Logic giống Portfolio.cshtml: Match close orders (reduceOnly=true) với open orders
                var orderDtos = new List<OrderDetailDto>();
                var processedOrders = new HashSet<int>();

                // Step 1: Find all close orders (reduceOnly = true)
                var closeOrders = orders.Where(o => o.ReduceOnly).ToList();

                foreach (var closeOrder in closeOrders)
                {
                    if (processedOrders.Contains(closeOrder.Id)) continue;

                    // Find matching open order: same symbol, same side, reduceOnly=false, earlier timestamp
                    var openOrder = orders
                        .Where(o => 
                            !processedOrders.Contains(o.Id) &&
                            o.Symbol == closeOrder.Symbol &&
                            o.Side == closeOrder.Side && // SAME SIDE (not opposite!)
                            !o.ReduceOnly &&
                            o.CreatedAt < closeOrder.CreatedAt)
                        .OrderBy(o => o.CreatedAt)
                        .FirstOrDefault();

                    if (openOrder != null)
                    {
                        // Found a pair! Create single trade with PnL
                        processedOrders.Add(openOrder.Id);
                        processedOrders.Add(closeOrder.Id);

                        var openFee = feeDict.ContainsKey(openOrder.Id.ToString()) ? feeDict[openOrder.Id.ToString()] : 0m;
                        var closeFee = feeDict.ContainsKey(closeOrder.Id.ToString()) ? feeDict[closeOrder.Id.ToString()] : 0m;
                        var totalFee = openFee + closeFee;

                        // Determine position type based on closeOrder.Side
                        // Buy close order = was SHORT position (Sell to open, Buy to close)
                        // Sell close order = was LONG position (Buy to open, Sell to close)
                        var positionType = closeOrder.Side == OrderSide.Buy ? "SHORT" : "LONG";

                        // Calculate PnL based on position type
                        var sideFactor = positionType == "LONG" ? 1m : -1m;
                        var pnl = (closeOrder.AvgPrice - openOrder.AvgPrice) * sideFactor * closeOrder.Quantity;

                        orderDtos.Add(new OrderDetailDto
                        {
                            Id = closeOrder.Id,
                            Symbol = closeOrder.Symbol,
                            Side = positionType, // LONG or SHORT
                            Type = $"{openOrder.Type}",
                            Size = closeOrder.Quantity,
                            EntryPrice = openOrder.AvgPrice,
                            ExitPrice = closeOrder.AvgPrice,
                            PnL = pnl,
                            Fee = totalFee,
                            Status = "Closed",
                            CreatedAt = openOrder.CreatedAt,
                            ClosedAt = closeOrder.UpdatedAt
                        });
                    }
                }

                // Step 2: Add remaining open orders (not yet closed)
                var remainingOrders = orders.Where(o => !processedOrders.Contains(o.Id) && !o.ReduceOnly).ToList();
                foreach (var openOrder in remainingOrders)
                {
                    var fee = feeDict.ContainsKey(openOrder.Id.ToString()) ? feeDict[openOrder.Id.ToString()] : 0m;
                    var positionType = openOrder.Side == OrderSide.Buy ? "LONG" : "SHORT";

                    orderDtos.Add(new OrderDetailDto
                    {
                        Id = openOrder.Id,
                        Symbol = openOrder.Symbol,
                        Side = positionType,
                        Type = openOrder.Type.ToString(),
                        Size = openOrder.Quantity,
                        EntryPrice = openOrder.AvgPrice,
                        ExitPrice = null,
                        PnL = 0m,
                        Fee = fee,
                        Status = openOrder.Status.ToString(),
                        CreatedAt = openOrder.CreatedAt,
                        ClosedAt = null
                    });
                }

                // Reverse to show newest first
                orderDtos.Reverse();

                var positions = await _context.Positions
                    .Where(p => p.UserWallet == userWallet)
                    .Select(p => new PositionDetailDto
                    {
                        Id = p.Id,
                        Symbol = p.Symbol,
                        Type = p.Side.ToString(),
                        Size = p.Size,
                        EntryPrice = p.EntryPrice,
                        Margin = p.Margin,
                        Leverage = p.Leverage,
                        UnrealizedPnL = p.UnrealizedPnl,
                        CreatedAt = p.CreatedAt
                    })
                    .ToListAsync();

                var transactions = await _context.BalanceTransactions
                    .Where(t => t.UserWallet == userWallet)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(100)
                    .Select(t => new BalanceTransactionDto
                    {
                        Id = t.Id,
                        Amount = t.Amount,
                        Type = t.Type,
                        Reference = t.Reference,
                        Description = t.Description,
                        BalanceAfter = t.BalanceAfter,
                        CreatedAt = t.CreatedAt
                    })
                    .ToListAsync();

                var withdrawals = await _context.WalletWithdrawalRequests
                    .Where(w => w.UserWallet == userWallet)
                    .OrderByDescending(w => w.CreatedAt)
                    .Select(w => new WithdrawalDetailDto
                    {
                        Id = w.Id,
                        RecipientAddress = w.RecipientAddress,
                        Amount = w.Amount,
                        Fee = w.Fee,
                        NetAmount = w.Amount - w.Fee,
                        Status = w.Status.ToString(),
                        ProcessedByAdmin = null,
                        CreatedAt = w.CreatedAt,
                        ProcessedAt = null
                    })
                    .ToListAsync();

                var activeLocks = await _context.Set<TradingAccountLock>()
                    .Where(l => l.UserWallet == userWallet && !l.IsUnlocked)
                    .OrderByDescending(l => l.LockedAt)
                    .Select(l => new TradingAccountLockDto
                    {
                        Id = l.Id,
                        LockType = l.LockType.ToString(),
                        Reason = l.Reason,
                        LockedByAdmin = l.LockedByAdmin,
                        LockedAt = l.LockedAt,
                        ExpiresAt = l.ExpiresAt
                    })
                    .ToListAsync();

                // Calculate aggregated stats
                var totalOrders = orderDtos.Count;
                var closedOrders = orderDtos.Count(o => o.Status == "Closed"); // Changed from "Filled"
                
                var totalFees = await _context.BalanceTransactions
                    .Where(t => t.UserWallet == userWallet && 
                           (t.Type == "TRADING_FEE" || t.Type == "WITHDRAWAL_FEE") && 
                           t.Amount < 0)
                    .SumAsync(t => (decimal?)(-t.Amount)) ?? 0;
                
                // TotalPnL = Sum of all closed trades PnL + UnrealizedPnL from open positions
                var realizedPnl = orderDtos.Where(o => o.Status == "Closed").Sum(o => o.PnL);
                var unrealizedPnl = positions.Sum(p => p.UnrealizedPnL);
                var totalPnL = realizedPnl + unrealizedPnl;
                
                // WinRate = (Winning Trades / Closed Trades) * 100
                var winningOrders = orderDtos.Count(o => o.Status == "Closed" && o.PnL > 0);
                var winRate = closedOrders > 0 ? (decimal)winningOrders / closedOrders * 100 : 0;
                
                var firstOrder = await _context.Orders
                    .Where(o => o.UserWallet == userWallet)
                    .OrderBy(o => o.CreatedAt)
                    .FirstOrDefaultAsync();
                
                var lastOrder = await _context.Orders
                    .Where(o => o.UserWallet == userWallet)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync();

                return new UserTradingDetailDto
                {
                    UserWallet = userWallet,
                    IsActive = activeLocks.Count == 0,
                    Balance = balance.Balance,
                    AvailableBalance = balance.AvailableBalance,
                    MarginUsed = balance.MarginUsed,
                    UnrealizedPnl = balance.UnrealizedPnl,
                    TotalPnL = totalPnL,
                    TotalFees = totalFees,
                    TotalOrders = totalOrders,
                    ClosedOrders = closedOrders,
                    OpenPositions = positions.Count,
                    WinRate = winRate,
                    WinningOrders = winningOrders,
                    TotalWithdrawals = withdrawals.Count,
                    FirstOrderDate = firstOrder?.CreatedAt,
                    LastOrderDate = lastOrder?.CreatedAt,
                    LastUpdated = balance.UpdatedAt,
                    Orders = orderDtos,
                    Positions = positions,
                    Transactions = transactions,
                    Withdrawals = withdrawals,
                    ActiveLocks = activeLocks
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user detail for {UserWallet}", userWallet);
                throw;
            }
        }

        public async Task<List<PendingWithdrawalDto>> GetPendingWithdrawalsAsync()
        {
            var feeConfig = await GetActiveFeeConfigAsync();
            var withdrawalFeePercent = (decimal)(feeConfig?.WithdrawalFeePercent ?? 0.5);

            var pending = await _context.WalletWithdrawalRequests
                .Where(w => w.Status == WithdrawalStatus.Pending)
                .OrderBy(w => w.CreatedAt)
                .ToListAsync();

            return pending.Select(w =>
            {
                var fee = Math.Max(w.Amount * withdrawalFeePercent / 100, (decimal)(feeConfig?.MinWithdrawalFee ?? 0.001));
                return new PendingWithdrawalDto
                {
                    Id = w.Id,
                    UserWallet = w.UserWallet,
                    RecipientAddress = w.RecipientAddress,
                    Amount = w.Amount,
                    Fee = fee,
                    NetAmount = w.Amount - fee,
                    CreatedAt = w.CreatedAt,
                    PendingDays = (DateTime.UtcNow - w.CreatedAt).Days
                };
            }).ToList();
        }

        public async Task<bool> ApproveWithdrawalAsync(ApproveWithdrawalRequest request, string adminWallet)
        {
            try
            {
                var withdrawal = await _context.WalletWithdrawalRequests
                    .FirstOrDefaultAsync(w => w.Id == request.WithdrawalId);

                if (withdrawal == null || withdrawal.Status != WithdrawalStatus.Pending)
                    return false;

                withdrawal.Status = WithdrawalStatus.Approved;
                withdrawal.AdminNotes = request.AdminNotes ?? $"Approved by {adminWallet}";

                await _context.SaveChangesAsync();

                _logger.LogInformation("Withdrawal {Id} approved by {Admin}", request.WithdrawalId, adminWallet);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving withdrawal {Id}", request.WithdrawalId);
                return false;
            }
        }

        public async Task<bool> RejectWithdrawalAsync(RejectWithdrawalRequest request, string adminWallet)
        {
            try
            {
                var withdrawal = await _context.WalletWithdrawalRequests
                    .FirstOrDefaultAsync(w => w.Id == request.WithdrawalId);

                if (withdrawal == null || withdrawal.Status != WithdrawalStatus.Pending)
                    return false;

                withdrawal.Status = WithdrawalStatus.Rejected;
                withdrawal.AdminNotes = $"Rejected by {adminWallet}: {request.Reason}";

                // Refund balance
                var userBalance = await _context.UserBalances
                    .FirstOrDefaultAsync(b => b.UserWallet == withdrawal.UserWallet);

                if (userBalance != null)
                {
                    userBalance.Balance += withdrawal.Amount;
                    userBalance.AvailableBalance += withdrawal.Amount;
                    userBalance.UpdatedAt = DateTime.UtcNow;

                    // Log transaction
                    var transaction = new BalanceTransaction
                    {
                        UserWallet = withdrawal.UserWallet,
                        Amount = withdrawal.Amount,
                        Type = "REFUND",
                        Reference = $"WITHDRAWAL_{withdrawal.Id}",
                        Description = $"Withdrawal rejected: {request.Reason}",
                        BalanceAfter = userBalance.Balance,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.BalanceTransactions.Add(transaction);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Withdrawal {Id} rejected by {Admin}", request.WithdrawalId, adminWallet);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting withdrawal {Id}", request.WithdrawalId);
                return false;
            }
        }

        public async Task<bool> LockAccountAsync(LockAccountRequest request, string adminWallet)
        {
            try
            {
                var lockRecord = new TradingAccountLock
                {
                    UserWallet = request.UserWallet,
                    LockType = request.LockType,
                    Reason = request.Reason,
                    LockedByAdmin = adminWallet,
                    LockedAt = DateTime.UtcNow,
                    ExpiresAt = request.ExpiresAt,
                    IsUnlocked = false
                };

                _context.Set<TradingAccountLock>().Add(lockRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Account {Wallet} locked by {Admin} - Type: {Type}", 
                    request.UserWallet, adminWallet, request.LockType);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking account {Wallet}", request.UserWallet);
                return false;
            }
        }

        public async Task<bool> UnlockAccountAsync(UnlockAccountRequest request, string adminWallet)
        {
            try
            {
                var lockRecord = await _context.Set<TradingAccountLock>()
                    .FirstOrDefaultAsync(l => l.Id == request.LockId);

                if (lockRecord == null || lockRecord.IsUnlocked)
                    return false;

                lockRecord.IsUnlocked = true;
                lockRecord.UnlockedByAdmin = adminWallet;
                lockRecord.UnlockedAt = DateTime.UtcNow;
                lockRecord.UnlockReason = request.Reason;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Account {Wallet} unlocked by {Admin}", 
                    lockRecord.UserWallet, adminWallet);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking account {LockId}", request.LockId);
                return false;
            }
        }

        public async Task<List<TradingAccountLock>> GetActiveLocksAsync()
        {
            return await _context.Set<TradingAccountLock>()
                .Where(l => !l.IsUnlocked)
                .OrderByDescending(l => l.LockedAt)
                .ToListAsync();
        }

        public async Task<bool> AdjustUserBalanceAsync(AdjustBalanceRequest request, string adminWallet)
        {
            try
            {
                var userBalance = await _context.UserBalances
                    .FirstOrDefaultAsync(b => b.UserWallet == request.UserWallet);

                if (userBalance == null)
                {
                    userBalance = new UserBalance
                    {
                        UserWallet = request.UserWallet,
                        Balance = 0,
                        AvailableBalance = 0,
                        MarginUsed = 0,
                        UnrealizedPnl = 0,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.UserBalances.Add(userBalance);
                }

                userBalance.Balance += request.Amount;
                userBalance.AvailableBalance += request.Amount;
                userBalance.UpdatedAt = DateTime.UtcNow;

                // Log transaction
                var transaction = new BalanceTransaction
                {
                    UserWallet = request.UserWallet,
                    Amount = request.Amount,
                    Type = "ADMIN_ADJUSTMENT",
                    Reference = $"ADMIN_{adminWallet}",
                    Description = request.Reason,
                    BalanceAfter = userBalance.Balance,
                    CreatedAt = DateTime.UtcNow
                };
                _context.BalanceTransactions.Add(transaction);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Balance adjusted for {Wallet} by {Admin}: {Amount}", 
                    request.UserWallet, adminWallet, request.Amount);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adjusting balance for {Wallet}", request.UserWallet);
                return false;
            }
        }

        public async Task<TradingFeeConfig?> GetActiveFeeConfigAsync()
        {
            return await _context.Set<TradingFeeConfig>()
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateFeeConfigAsync(UpdateFeeConfigRequest request, string adminWallet)
        {
            try
            {
                // Deactivate old config
                var oldConfigs = await _context.Set<TradingFeeConfig>()
                    .Where(c => c.IsActive)
                    .ToListAsync();

                foreach (var old in oldConfigs)
                {
                    old.IsActive = false;
                }

                // Create new config
                var newConfig = new TradingFeeConfig
                {
                    Name = $"Config_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                    MakerFeePercent = request.MakerFeePercent,
                    TakerFeePercent = request.TakerFeePercent,
                    WithdrawalFeePercent = request.WithdrawalFeePercent,
                    MinWithdrawalFee = request.MinWithdrawalFee,
                    MinWithdrawalAmount = request.MinWithdrawalAmount,
                    MaxWithdrawalAmount = request.MaxWithdrawalAmount,
                    DailyWithdrawalLimit = request.DailyWithdrawalLimit,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Notes = request.Notes ?? $"Updated by {adminWallet}"
                };

                _context.Set<TradingFeeConfig>().Add(newConfig);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Fee config updated by {Admin}", adminWallet);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating fee config");
                return false;
            }
        }

        public async Task<byte[]> ExportTradingReportAsync(DateTime fromDate, DateTime toDate)
        {
            // TODO: Implement CSV/Excel export
            throw new NotImplementedException("Export feature will be implemented later");
        }
    }
}
