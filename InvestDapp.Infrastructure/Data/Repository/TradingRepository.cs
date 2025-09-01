using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Models.Trading;
using Microsoft.EntityFrameworkCore;

namespace InvestDapp.Infrastructure.Data.Repository
{
    public class TradingRepository : ITradingRepository
    {
        private readonly InvestDbContext _db;

        public TradingRepository(InvestDbContext db)
        {
            _db = db;
        }

        public async Task AddOrderAsync(Order order)
        {
            _db.Orders.Add(order);
            await Task.CompletedTask;
        }

        public async Task<Order?> GetOrderByInternalIdAsync(string internalId)
        {
            return await _db.Orders.FirstOrDefaultAsync(o => o.InternalOrderId == internalId);
        }

        public async Task<List<Order>> GetOrdersByUserAsync(string userWallet)
        {
            return await _db.Orders.Where(o => o.UserWallet == userWallet).OrderByDescending(o => o.CreatedAt).ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }

        public async Task<UserBalance?> GetUserBalanceAsync(string userWallet)
        {
            return await _db.UserBalances.FirstOrDefaultAsync(b => b.UserWallet == userWallet);
        }

        public async Task AddOrUpdateUserBalanceAsync(UserBalance bal)
        {
            var existing = await _db.UserBalances.FirstOrDefaultAsync(b => b.UserWallet == bal.UserWallet);
            if (existing == null)
            {
                _db.UserBalances.Add(bal);
            }
            else
            {
                existing.Balance = bal.Balance;
                existing.AvailableBalance = bal.AvailableBalance;
                existing.MarginUsed = bal.MarginUsed;
                existing.UnrealizedPnl = bal.UnrealizedPnl;
                existing.UpdatedAt = bal.UpdatedAt;
            }
        }

        public async Task AddBalanceTransactionAsync(BalanceTransaction tx)
        {
            _db.BalanceTransactions.Add(tx);
            await Task.CompletedTask;
        }

        public async Task<List<Position>> GetPositionsByUserSymbolAsync(string userWallet, string symbol)
        {
            return await _db.Positions.Where(p => p.UserWallet == userWallet && p.Symbol == symbol).OrderBy(p => p.CreatedAt).ToListAsync();
        }

        public async Task<List<Position>> GetPositionsByUserAsync(string userWallet)
        {
            return await _db.Positions.Where(p => p.UserWallet == userWallet).OrderBy(p => p.CreatedAt).ToListAsync();
        }

        public async Task<Position?> GetPositionByIdAsync(int id)
        {
            return await _db.Positions.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task AddPositionAsync(Position p)
        {
            _db.Positions.Add(p);
            await Task.CompletedTask;
        }

        public async Task RemovePositionAsync(Position p)
        {
            _db.Positions.Remove(p);
            await Task.CompletedTask;
        }

        public async Task<Position> UpsertPositionAsync(Position p)
            {
                if (p.Id > 0)
                {
                    var existing = await _db.Positions.FirstOrDefaultAsync(x => x.Id == p.Id);
                    if (existing != null)
                    {
                        existing.Side = p.Side;
                        existing.Size = p.Size;
                        existing.EntryPrice = p.EntryPrice;
                        existing.MarkPrice = p.MarkPrice;
                        existing.UnrealizedPnl = p.UnrealizedPnl;
                        existing.RealizedPnl = p.RealizedPnl;
                        existing.Leverage = p.Leverage;
                        existing.Margin = p.Margin;
                        existing.PnL = p.PnL;
                        existing.TakeProfitPrice = p.TakeProfitPrice;
                        existing.StopLossPrice = p.StopLossPrice;
                        existing.MaintenanceMarginRate = p.MaintenanceMarginRate;
                        existing.IsIsolated = p.IsIsolated;
                        existing.LiquidationPrice = p.LiquidationPrice;
                        existing.UpdatedAt = p.UpdatedAt;
                        await Task.CompletedTask;
                        return existing;
                    }
                }
                _db.Positions.Add(p);
                await _db.SaveChangesAsync();
                return p;
            }
        }
}
