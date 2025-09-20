using InvestDapp.Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface INotificationRepository
    {
        Task<Notification> CreateAsync(Notification notification);
        Task<ICollection<Notification>> GetByUserAsync(int userId);
        Task<bool> MarkAsReadAsync(int userId, int notificationId);
        Task<int> GetUnreadCountAsync(int userId);
    }
}
