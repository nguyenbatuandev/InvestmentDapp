using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Common;

namespace InvestDapp.Application.NotificationService
{
    public interface INotificationService
    {
        Task<BaseResponse<object>> CreateNotificationAsync(CreateNotificationRequest req);
        Task SendUnreadCountUpdateAsync(string userId, int unreadCount);
        Task<BaseResponse<object>> CreateNotificationForCampaignInvestorsAsync(InvestDapp.Shared.Common.Request.CreateNotificationToCampaignRequest req);
    }
}
