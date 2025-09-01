using InvestDapp.Shared.Models.Trading;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface ITradingRepository
    {
        Task AddOrderAsync(Order order);
        Task<Order?> GetOrderByInternalIdAsync(string internalId);
        Task<List<Order>> GetOrdersByUserAsync(string userWallet);
        Task SaveChangesAsync();

        Task<UserBalance?> GetUserBalanceAsync(string userWallet);
        Task AddOrUpdateUserBalanceAsync(UserBalance bal);

        Task AddBalanceTransactionAsync(BalanceTransaction tx);

        Task<List<Position>> GetPositionsByUserSymbolAsync(string userWallet, string symbol);
        Task<List<Position>> GetPositionsByUserAsync(string userWallet);
        Task<Position?> GetPositionByIdAsync(int id);
        Task AddPositionAsync(Position p);
        Task RemovePositionAsync(Position p);
        Task<Position> UpsertPositionAsync(Position p);
    }
}
