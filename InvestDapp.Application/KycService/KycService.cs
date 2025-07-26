using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models.Kyc;


namespace InvestDapp.Application.KycService
{
    public class KycService : IKycService
    {
        private readonly IKyc _kycRepository;
        private readonly IUser _userRepository;
        public KycService(IKyc kycRepository, IUser userRepository)
        {
            _kycRepository = kycRepository;
            _userRepository = userRepository;
        }

        public async Task<BaseResponse<FundraiserKyc>> CheckKycAsync(string walletAddress)
        {
            var latestFundraiserKyc = await _kycRepository.GetLatestFundraiserKycByWalletAsync(walletAddress); 
            if (latestFundraiserKyc != null)
            {
                if (latestFundraiserKyc.IsApproved == null)
                {
                    return new BaseResponse<FundraiserKyc>
                    {
                        Success = true,
                        Message = "1",
                        Data = null,
                        Errors = null
                    };
                }

                if (latestFundraiserKyc.IsApproved == true)
                {
                    return new BaseResponse<FundraiserKyc>
                    {
                        Success = true,
                        Message = "2",
                        Data = null
                    };
                }

                if (latestFundraiserKyc.IsApproved == false)
                {
                    return new BaseResponse<FundraiserKyc>
                    {
                        Success = true,
                        Message = "3",
                        Data = null
                    };
                }
            }
            return new BaseResponse<FundraiserKyc>
            {
                Success = true,
                Data = null,
                Message = "0",
                
            };
        }

        public async Task<BaseResponse<FundraiserKyc>> SubmitKycAsync(FundraiserKycRequest kycRequest, string walletAddress)
        {
            // 1. Tìm kiếm người dùng bằng địa chỉ ví

            var latestFundraiserKyc =await _kycRepository.GetLatestFundraiserKycByWalletAsync(walletAddress);

            var user = await _userRepository.GetUserByWalletAddressAsync(walletAddress);

            if (latestFundraiserKyc != null)
            {
                if (latestFundraiserKyc.IsApproved == null)
                {
                    return new BaseResponse<FundraiserKyc>
                    {
                        Success = false,
                        Message = "Bạn đã gửi yêu cầu KYC trước đó và đang chờ duyệt.",
                        Data = null
                    };
                }

                if (latestFundraiserKyc.IsApproved == true)
                {
                    return new BaseResponse<FundraiserKyc>
                    {
                        Success = false,
                        Message = "Bạn đã được phê duyệt KYC trước đó.",
                        Data = null
                    };
                }
            }

            if (user == null)
            {
                return new BaseResponse<FundraiserKyc>
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng với địa chỉ ví này.",
                    Data = null
                };
            }

            await _kycRepository.SubmitKycAsync(kycRequest, user.ID);
            return new BaseResponse<FundraiserKyc>
            {
                Success = true,
                Message = "Yêu cầu KYC đã được gửi thành công.",
                Data = null
            };
        }
    }
}
