using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models;

namespace InvestDapp.Application.UserService
{
    public interface IUserService
    {
        Task<BaseResponse<User>>UpdateUserAsync(UserUpdateRequest userUpdate , string wallet);
        Task<BaseResponse<User>> GetUserByWalletAddressAsync(string walletAddres);
        Task<BaseResponse<User>> GetUserByIdAsync(int id);
        Task<int> GetCurrentUserId();
        Task<BaseResponse<ICollection<Notification>>> GetNotificationsAsync(int userId);
        Task<BaseResponse<bool>> MarkNotificationAsReadAsync(int userId, int notificationId);
        Task<BaseResponse<bool>> DeleteReadNotificationsAsync(int userId);

    }
}
