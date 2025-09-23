using InvestDapp.Models;
using InvestDapp.Shared.DTOs;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models;


namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface ICampaign
    {
        Task <Campaign> UpdateCampaignStatusAsync(Campaign campaign);
        Task<Campaign?> GetCampaignByIdAsync(int? id);
        Task<IEnumerable<Campaign>> GetAllCampaignsAsync();
        Task<Campaign> CreateCampaignAsync(Campaign campaign);
        Task<IEnumerable<Campaign>> GetCampaignsByOwnerAsync(string ownerAddress);
        Task<IEnumerable<Campaign>> GetCampaignsByInvestorAsync(string investorAddress);

        // Campaign Services with approval Administration
        Task<IEnumerable<Campaign>> GetCampaignsForAdminAsync(CampaignStatus? status = null, ApprovalStatus? approvalStatus = null, int page = 1, int pageSize = 10);
        Task<WithdrawalRequest> CreatRerequestWithdrawalAsync(WithdrawalRequestDto withdrawalRequestDto);
        Task<(WithdrawalRequest, int)> UpdateWithdrawalRequestStatusAsync(UpdateWithdrawalStatusDto dto);
        Task<Refund> ClaimRefundAsync(ClaimRefundDto refundDto);
    }
}
