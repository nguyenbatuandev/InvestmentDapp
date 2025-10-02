using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.DTOs.Admin;
using InvestDapp.Shared.Models.Kyc;

namespace InvestDapp.Application.KycService
{
    public interface IKycService
    {
        Task<BaseResponse<FundraiserKyc>> SubmitKycAsync(FundraiserKycRequest kycRequest, string walletAddress);
        Task<BaseResponse<FundraiserKyc>> CheckKycAsync(string walletAddress);
        Task<BaseResponse<PagedResult<AdminKycItemDto>>> QueryKycsAsync(KycAdminFilterRequest filterRequest);
        Task<BaseResponse<bool>> ApproveKycAsync(int kycId);
        Task<BaseResponse<bool>> RejectKycAsync(int kycId);
        Task<BaseResponse<bool>> RevokeKycAsync(int kycId);
    }
}
