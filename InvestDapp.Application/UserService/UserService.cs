using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models;
using Microsoft.AspNetCore.Http;


namespace InvestDapp.Application.UserService
{
    public class UserService : IUserService
    {
        private readonly IUser _userRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(IUser userRepository, IHttpContextAccessor _httpContextAccessor)
        {
            _userRepository = userRepository;
            this._httpContextAccessor = _httpContextAccessor;
        }

        public async Task<int> GetCurrentUserId()
        {
            var wallet = _httpContextAccessor.HttpContext?.User?.FindFirst("WalletAddress")?.Value;

            if (string.IsNullOrEmpty(wallet))
            {
                throw new UnauthorizedAccessException("Không tìm thấy WalletAddress trong claim.");
            }

            var user = await _userRepository.GetUserByWalletAddressAsync(wallet);

            if (user == null)
            {
                throw new Exception($"Không tìm thấy người dùng với ví: {wallet}");
            }

            return user.ID;
        }

        public async Task<BaseResponse<User>> GetUserByIdAsync(int id)
        {
            var user = await _userRepository.GetUserByIdAsync(id);
            if (user == null)
            {
                return new BaseResponse<User>
                {
                    Success = false,
                    Message = "User not found"
                };
            }
            return new BaseResponse<User>
            {
                Success = true,
                Data = user
            };
        }

        public async Task<BaseResponse<ICollection<Notification>>> GetNotificationsAsync(int userId)
        {
            try
            {
                var data = await _userRepository.GetNotificationsAsync(userId);
                return new BaseResponse<ICollection<Notification>> { Success = true, Data = data };
            }
            catch (Exception ex)
            {
                return new BaseResponse<ICollection<Notification>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<BaseResponse<bool>> MarkNotificationAsReadAsync(int userId, int notificationId)
        {
            try
            {
                    var ok = await _userRepository.MarkNotificationAsReadAsync(userId, notificationId);
                return new BaseResponse<bool> { Success = ok, Data = ok };
            }
            catch (Exception ex)
            {
                return new BaseResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<BaseResponse<User>> GetUserByWalletAddressAsync(string walletAddress)
        {
            var rl =await _userRepository.GetUserByWalletAddressAsync(walletAddress);
            if (rl == null)
            {
                return new BaseResponse<User>
                {
                    Success = false,
                    Message = "User not found"
                };
            }
            return new BaseResponse<User>
            {
                Success = true,
                Data = rl
            };

        }

        public async Task<BaseResponse<User>> UpdateUserAsync(UserUpdateRequest userUpdate , string walletAddress)
        {
            var rl = await _userRepository.GetUserByWalletAddressAsync(walletAddress);
            if (rl == null)
            {
                return new BaseResponse<User>
                {
                    Success = false,
                    Message = "User not found"
                };
            }
            var updatedUser = await _userRepository.UpdateUserAsync(userUpdate, walletAddress);
            if (updatedUser == null)
            {
                return new BaseResponse<User>
                {
                    Success = false,
                    Message = "Failed to update user"
                };
            }
            return new BaseResponse<User>
            {
                Success = true,
                Message = "User updated successfully",
                Data = updatedUser
            };
        }

        public async Task<BaseResponse<bool>> DeleteReadNotificationsAsync(int userId)
        {
            try
            {
                var success = await _userRepository.DeleteReadNotificationsAsync(userId);
                return new BaseResponse<bool> { Success = success, Data = success };
            }
            catch (Exception ex)
            {
                return new BaseResponse<bool> { Success = false, Message = ex.Message };
            }
        }
    }
}
