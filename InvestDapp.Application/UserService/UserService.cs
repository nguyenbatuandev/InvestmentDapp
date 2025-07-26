using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models;


namespace InvestDapp.Application.UserService
{
    public class UserService : IUserService
    {
        private readonly IUser _userRepository;
        public UserService(IUser userRepository)
        {
            _userRepository = userRepository;
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

        public Task<BaseResponse<User>> GetUserByWalletAddressAsync(string walletAddress, string wallet)
        {
            throw new NotImplementedException();
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
    }
}
