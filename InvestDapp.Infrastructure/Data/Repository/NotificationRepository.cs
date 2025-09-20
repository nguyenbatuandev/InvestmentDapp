using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InvestDapp.Infrastructure.Data.Repository
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly InvestDbContext _context;
        private readonly Microsoft.Extensions.Logging.ILogger<NotificationRepository> _logger;
        public NotificationRepository(InvestDbContext context)
        {
            _context = context;
            // logger will be provided by DI if available
            _logger = null!;
        }

        // Constructor overload to accept logger (keeps backwards compatible DI resolution)
        public NotificationRepository(InvestDbContext context, Microsoft.Extensions.Logging.ILogger<NotificationRepository> logger) : this(context)
        {
            _logger = logger;
        }

        public async Task<Notification> CreateAsync(Notification notification)
        {
            try
            {
                _logger?.LogInformation("Saving notification for UserId={UserId}", notification.UserID);
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                _logger?.LogInformation("Saved notification Id={NotificationId} for UserId={UserId}", notification.ID, notification.UserID);
                return notification;
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "Error saving notification for UserId={UserId}", notification.UserID);
                throw;
            }
        }

        public async Task<ICollection<Notification>> GetByUserAsync(int userId)
        {
            return await _context.Notifications.Where(n => n.UserID == userId).OrderByDescending(n => n.CreatedAt).ToListAsync();
        }

        public async Task<bool> MarkAsReadAsync(int userId, int notificationId)
        {
            var n = await _context.Notifications.FirstOrDefaultAsync(x => x.ID == notificationId && x.UserID == userId);
            if (n == null) return false;
            if (n.IsRead) return true;
            n.IsRead = true;
            _context.Notifications.Update(n);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications.CountAsync(n => n.UserID == userId && !n.IsRead);
        }
    }
}
