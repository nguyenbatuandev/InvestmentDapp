using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models.Kyc;

namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface IKyc
    {
        Task<FundraiserKyc> SubmitKycAsync(FundraiserKycRequest kycRequest , int id);
        Task<FundraiserKyc?> GetLatestFundraiserKycByWalletAsync(string walletAddress);
        Task<(IReadOnlyList<FundraiserKyc> Items, int TotalCount)> QueryKycsAsync(string? status, string? accountType, string? searchTerm, int page, int pageSize);
        Task<FundraiserKyc?> GetKycByIdAsync(int id);
        Task<bool> UpdateKycStatusAsync(int id, bool? isApproved);
    }
}
