using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Enums;
using InvestDapp.Models;

namespace InvestDapp.Application.CampaignService
{
    public interface ICampaignPostService
    {
        // Campaign Post Services
        Task<CampaignPost> CreatePostAsync(CreateCampaignPostRequest request, string authorAddress);
        Task<CampaignPost?> GetPostByIdAsync(int id);
        Task<IEnumerable<CampaignPost>> GetPostsByCampaignIdAsync(int campaignId);
        Task<IEnumerable<CampaignPost>> GetPendingPostsAsync();
        Task<IEnumerable<CampaignPost>> GetApprovedPostsAsync(int page = 1, int pageSize = 10);
        Task<bool> ApprovePostAsync(int id, string adminNotes, string adminWallet);
        Task<bool> RejectPostAsync(int id, string adminNotes, string adminWallet);
        Task<bool> UpdatePostAsync(int id, CreateCampaignPostRequest request, string authorAddress);
        Task<bool> DeletePostAsync(int id, string authorAddress);

        // Campaign Services with approval
        Task<Campaign> CreateCampaignAsync(CreateCampaignRequest request, string ownerAddress);
        Task<IEnumerable<Campaign>> GetPendingCampaignsAsync();
        Task<Campaign?> GetCampaignByIdAsync(int id);
        Task<bool> ApproveCampaignAsync(int id, string adminNotes, string adminWallet);
        Task<bool> RejectCampaignAsync(int id, string adminNotes, string adminWallet);
        Task<IEnumerable<Campaign>> GetUserCampaignsAsync(string ownerAddress);
        Task<bool> CanUserEditCampaign(int campaignId, string userAddress);
        Task<bool> CanUserCreatePost(int campaignId, string userAddress);
    }
}