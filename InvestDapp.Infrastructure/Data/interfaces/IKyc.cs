using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models.Kyc;

namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface IKyc
    {
        Task<FundraiserKyc> SubmitKycAsync(FundraiserKycRequest kycRequest , int id);
        Task<FundraiserKyc?> GetLatestFundraiserKycByWalletAsync(string walletAddress);
    }
}
