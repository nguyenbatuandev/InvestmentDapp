using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models;

namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface IUser
    {
        Task<User?> CreateUserAsync(string wallet, string name, string email);
        Task<User> UpdateUserAsync(UserUpdateRequest userUpdate , string wallet);
        Task<User?> GetUserByWalletAddressAsync(string walletAddress);
        Task<User> SetRoleByIdAsync(string walletAddress);
    }
}
