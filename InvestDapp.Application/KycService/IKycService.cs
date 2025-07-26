using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models.Kyc;

namespace InvestDapp.Application.KycService
{
    public interface IKycService
    {
        Task<BaseResponse<FundraiserKyc>> SubmitKycAsync(FundraiserKycRequest kycRequest, string walletAddress);
        Task<BaseResponse<FundraiserKyc>> CheckKycAsync(string walletAddress);
    }
}
