using InvestDapp.Models;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Enums;

namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface ICampaignPostRepository
    {
        // Campaign Post methods
        Task<CampaignPost> CreatePostAsync(CampaignPost post);
        Task<CampaignPost?> GetPostByIdAsync(int id);
        Task<IEnumerable<CampaignPost>> GetPostsByCampaignIdAsync(int campaignId);
        Task<IEnumerable<CampaignPost>> GetPostsByStatusAsync(ApprovalStatus status);
        Task<IEnumerable<CampaignPost>> GetPendingPostsAsync();
        Task<IEnumerable<CampaignPost>> GetApprovedPostsAsync(int skip = 0, int take = 10);
        Task<bool> UpdatePostAsync(CampaignPost post);
        Task<bool> DeletePostAsync(int id);
        Task<bool> ApprovePostAsync(int id, string adminNotes, string approvedBy);
        Task<bool> RejectPostAsync(int id, string adminNotes, string approvedBy);
        
        // Campaign methods with approval
        Task<bool> ApproveCampaignAsync(int id, string adminNotes, string approvedBy);
        Task<bool> RejectCampaignAsync(int id, string adminNotes, string approvedBy);
        Task<IEnumerable<Campaign>> GetPendingCampaignsAsync();
        Task<IEnumerable<Campaign>> GetApprovedCampaignsAsync();
        Task<Campaign?> GetCampaignByIdAsync(int id);
        Task<bool> UpdateCampaignAsync(Campaign campaign);
    }
}