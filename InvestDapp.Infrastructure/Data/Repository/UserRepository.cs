using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Infrastructure.Data.Repository
{
    public class UserRepository : IUser
    {
        private readonly InvestDbContext _context;
        public UserRepository(InvestDbContext context)
        {
            _context = context;
        }

        public async Task<User?> CreateUserAsync(string wallet, string name, string email)
        {
            try
            {
                var user = new User
                {
                    WalletAddress = wallet,
                    Email = email, // sửa đúng
                    Name = name,   // sửa đúng
                    Role = "User",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return user;
            }
            catch (Exception ex)
            {
                // Bạn có thể log lỗi ở đây
                return null;
            }
        }
        public async Task<User?> GetUserByWalletAddressAsync(string wallet)
        {
            var normalizedWallet = wallet?.Trim().ToLower();
            if (string.IsNullOrEmpty(normalizedWallet)) return null;

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.WalletAddress.ToLower() == normalizedWallet);
            return user;
        }


        public async Task<User> SetRoleByIdAsync(string walletAddress)
        {
            var user = _context.Users.FirstOrDefault(u => u.WalletAddress == walletAddress);
            if (user == null)
            {
                throw new Exception("User not found");
            }
            user.Role = "Admin";
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User?> UpdateUserAsync(UserUpdateRequest userUpdate , string wallet)
        {
            var user = _context.Users.FirstOrDefault(u => u.WalletAddress == wallet);
            if (!string.IsNullOrWhiteSpace(userUpdate.Name))
            {
                user.Name = userUpdate.Name;
            }
            if (!string.IsNullOrWhiteSpace(userUpdate.Email))
            {
                user.Email = userUpdate.Email;
            }
            if (!string.IsNullOrWhiteSpace(userUpdate.Avatar))
            {
                user.Avatar = userUpdate.Avatar;
            }
            if (!string.IsNullOrWhiteSpace(userUpdate.Bio))
            {
                user.Bio = userUpdate.Bio;
            }
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }
    }
}
