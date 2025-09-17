using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Enums;
using InvestDapp.Models;
using InvestDapp.Shared.DTOs;

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
        Task<IEnumerable<Campaign>> GetApprovedCampaignsAsync();
        Task<Campaign?> GetCampaignByIdAsync(int id);
        Task<IEnumerable<Campaign>> GetUserCampaignsAsync(string ownerAddress);
        Task<bool> UpdateCampaignAsync(Campaign campaign);
        Task<bool> CanUserEditCampaign(int campaignId, string userAddress);
        Task<bool> CanUserCreatePost(int campaignId, string userAddress);

        // Campaign Administration
        Task<bool> ApproveCampaignAsync(int id, string adminWallet, string? adminNotes = null);
        Task<bool> RejectCampaignAsync(int id, string adminWallet, string adminNotes);
        Task<IEnumerable<Campaign>> GetCampaignsForAdminAsync(CampaignStatus? status = null, ApprovalStatus? approvalStatus = null, int page = 1, int pageSize = 10);


    }
}