using InvestDapp.Models;
using InvestDapp.Shared.Enums;


namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface ICampaign
    {
        Task <Campaign> UpdateCampaignStatusAsync(Campaign campaign);
        Task<Campaign?> GetCampaignByIdAsync(int? id);
        Task<IEnumerable<Campaign>> GetAllCampaignsAsync();
        Task<Campaign> CreateCampaignAsync(Campaign campaign);
        Task<IEnumerable<Campaign>> GetCampaignsByOwnerAsync(string ownerAddress);

        // Campaign Services with approval Administration
        Task<IEnumerable<Campaign>> GetCampaignsForAdminAsync(CampaignStatus? status = null, ApprovalStatus? approvalStatus = null, int page = 1, int pageSize = 10);
    }
}
