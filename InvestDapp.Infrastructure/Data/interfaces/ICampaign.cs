using InvestDapp.Models;


namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface ICampaign
    {
        Task <Campaign> UpdateCampaignStatusAsync(Campaign campaign);
        Task<Campaign?> GetCampaignByIdAsync(int? id);
        Task<IEnumerable<Campaign>> GetAllCampaignsAsync();
        Task<Campaign> CreateCampaignAsync(Campaign campaign);
        Task<IEnumerable<Campaign>> GetCampaignsByOwnerAsync(string ownerAddress);
    }
}
