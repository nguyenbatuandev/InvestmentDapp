using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models;
using Microsoft.EntityFrameworkCore;


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
                    Email = email,
                    Name = name, 
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
                return null;
            }
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.ID == id);
        }

        public async Task<ICollection<InvestDapp.Shared.Models.Notification>> GetNotificationsAsync(int userId)
        {
            var notis = await _context.Notifications
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            return notis;
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

        public async Task<bool> MarkNotificationAsReadAsync(int userId, int notificationId)
        {
            var n = await _context.Notifications.FirstOrDefaultAsync(x => x.ID == notificationId && x.UserID == userId);
            if (n == null) return false;
            if (n.IsRead) return true;
            n.IsRead = true;
            _context.Notifications.Update(n);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteReadNotificationsAsync(int userId)
        {
            try
            {
                var readNotifications = await _context.Notifications
                    .Where(n => n.UserID == userId && n.IsRead)
                    .ToListAsync();

                if (readNotifications.Any())
                {
                    _context.Notifications.RemoveRange(readNotifications);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
