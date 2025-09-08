using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Enums;
using InvestDapp.Models;

namespace InvestDapp.Application.CampaignService
{
    public class CampaignPostService : ICampaignPostService
    {
        private readonly ICampaignPostRepository _repository;
        private readonly ICampaign _campaignRepository;
        private readonly IUser _userRepository;

        public CampaignPostService(ICampaignPostRepository repository, ICampaign campaignRepository, IUser userRepository)
        {
            _repository = repository;
            _campaignRepository = campaignRepository;
            _userRepository = userRepository;
        }

        #region Campaign Post Services

        public async Task<CampaignPost> CreatePostAsync(CreateCampaignPostRequest request, string authorAddress)
        {
            // Kiểm tra quyền tạo bài viết
            if (!await CanUserCreatePost(request.CampaignId, authorAddress))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền tạo bài viết cho chiến dịch này.");
            }

            var campaign = await _repository.GetCampaignByIdAsync(request.CampaignId);
            var existingPosts = await _repository.GetPostsByCampaignIdAsync(request.CampaignId);
            
            // Kiểm tra xem đây có phải là bài viết đầu tiên không
            bool isFirstPost = !existingPosts.Any();

            var post = new CampaignPost
            {
                CampaignId = request.CampaignId,
                Title = request.Title,
                Content = request.Content,
                PostType = request.PostType,
                ImageUrl = request.ImageUrl,
                AuthorAddress = authorAddress,
                Tags = request.Tags,
                IsFeatured = request.IsFeatured,
                ApprovalStatus = isFirstPost ? ApprovalStatus.Pending : ApprovalStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                ViewCount = 0,
                ApprovedAt = isFirstPost ? null : DateTime.UtcNow,
                ApprovedBy = isFirstPost ? null : "SYSTEM_AUTO_APPROVE"
            };

            var result = await _repository.CreatePostAsync(post);

            // Cập nhật trạng thái campaign nếu đây là bài viết đầu tiên (vẫn giữ như trước)
            if (isFirstPost && campaign != null)
            {
                campaign.Status = CampaignStatus.PendingApproval;
                await _repository.UpdateCampaignAsync(campaign);
            }

            return result;
        }

        public async Task<CampaignPost?> GetPostByIdAsync(int id)
        {
            return await _repository.GetPostByIdAsync(id);
        }

        public async Task<IEnumerable<CampaignPost>> GetPostsByCampaignIdAsync(int campaignId)
        {
            return await _repository.GetPostsByCampaignIdAsync(campaignId);
        }

        public async Task<IEnumerable<CampaignPost>> GetPendingPostsAsync()
        {
            return await _repository.GetPendingPostsAsync();
        }

        public async Task<IEnumerable<CampaignPost>> GetApprovedPostsAsync(int page = 1, int pageSize = 10)
        {
            var skip = (page - 1) * pageSize;
            return await _repository.GetApprovedPostsAsync(skip, pageSize);
        }

        public async Task<bool> ApprovePostAsync(int id, string adminNotes, string adminWallet)
        {
            return await _repository.ApprovePostAsync(id, adminNotes, adminWallet);
        }

        public async Task<bool> RejectPostAsync(int id, string adminNotes, string adminWallet)
        {
            return await _repository.RejectPostAsync(id, adminNotes, adminWallet);
        }

        public async Task<bool> UpdatePostAsync(int id, CreateCampaignPostRequest request, string authorAddress)
        {
            var post = await _repository.GetPostByIdAsync(id);
            if (post == null) return false;

            if (post.AuthorAddress.ToLower() != authorAddress.ToLower())
            {
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bài viết này.");
            }

            if (post.ApprovalStatus == ApprovalStatus.Approved)
            {
                throw new InvalidOperationException("Không thể chỉnh sửa bài viết đã được duyệt.");
            }

            post.Title = request.Title;
            post.Content = request.Content;
            post.PostType = request.PostType;
            post.ImageUrl = request.ImageUrl;
            post.Tags = request.Tags;
            post.IsFeatured = request.IsFeatured;
            post.ApprovalStatus = ApprovalStatus.Pending;
            post.UpdatedAt = DateTime.UtcNow;

            return await _repository.UpdatePostAsync(post);
        }

        public async Task<bool> DeletePostAsync(int id, string userAddress)
        {
            var post = await _repository.GetPostByIdAsync(id);
            if (post == null) return false;

            var user = await _userRepository.GetUserByWalletAddressAsync(userAddress);
            bool isAdmin = user?.Role == "Admin";

            if (!isAdmin && post.AuthorAddress.ToLower() != userAddress.ToLower())
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xóa bài viết này.");
            }

            return await _repository.DeletePostAsync(id);
        }

        #endregion

        #region Campaign Services

        public async Task<Campaign> CreateCampaignAsync(CreateCampaignRequest request, string ownerAddress)
        {
            var campaign = new Campaign
            {
                OwnerAddress = ownerAddress,
                Name = request.Name,
                ShortDescription = request.ShortDescription,
                Description = request.Description,
                GoalAmount = request.GoalAmount,
                EndTime = request.EndTime,
                ImageUrl = request.ImageUrl,
                categoryId = request.CategoryId,
                ApprovalStatus = ApprovalStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Status = CampaignStatus.PendingPost, // Bắt đầu với trạng thái chờ bài viết
                CurrentRaisedAmount = 0,
                TotalInvestmentsOnCompletion = 0,
                TotalProfitAdded = 0,
                InvestorCount = 0,
                DeniedWithdrawalRequestCount = 0
            };

            var result = await _campaignRepository.CreateCampaignAsync(campaign);
            return result;
        }

        public async Task<IEnumerable<Campaign>> GetPendingCampaignsAsync()
        {
            return await _repository.GetPendingCampaignsAsync();
        }

        public async Task<IEnumerable<Campaign>> GetApprovedCampaignsAsync()
        {
            return await _repository.GetApprovedCampaignsAsync();
        }

        public async Task<Campaign?> GetCampaignByIdAsync(int id)
        {
            return await _repository.GetCampaignByIdAsync(id);
        }

        public async Task<bool> ApproveCampaignAsync(int id, string adminWallet, string? adminNotes = null)
        {
            var campaign = await _repository.GetCampaignByIdAsync(id);
            var posts = await _repository.GetPostsByCampaignIdAsync(id);
            var firstPost = posts.OrderBy(p => p.CreatedAt).FirstOrDefault();
            if (campaign == null) return false;

            // Cập nhật trạng thái campaign khi được approve
            campaign.Status = CampaignStatus.Active;
            firstPost.ApprovalStatus = ApprovalStatus.Approved;
            var result = await _repository.ApproveCampaignAsync(id, adminNotes, adminWallet);
            return result;
        }

        public async Task<bool> RejectCampaignAsync(int id, string adminWallet, string adminNotes)
        {
            var campaign = await _repository.GetCampaignByIdAsync(id);
            if (campaign == null) return false;

            campaign.Status = CampaignStatus.Failed;

            var posts = await _repository.GetPostsByCampaignIdAsync(id);
            var firstPost = posts.OrderBy(p => p.CreatedAt).FirstOrDefault();
            
            if (firstPost != null && firstPost.ApprovedBy == "SYSTEM_AUTO_APPROVE")
            {
                await _repository.DeletePostAsync(firstPost.Id);
            }

            return await _repository.RejectCampaignAsync(id, adminNotes, adminWallet);
        }

        public async Task<IEnumerable<Campaign>> GetCampaignsForAdminAsync(CampaignStatus? status = null, ApprovalStatus? approvalStatus = null, int page = 1, int pageSize = 10)
        {
            return await _campaignRepository.GetCampaignsForAdminAsync(status, approvalStatus, page, pageSize);
        }

        public async Task<IEnumerable<Campaign>> GetUserCampaignsAsync(string ownerAddress)
        {
            return await _campaignRepository.GetCampaignsByOwnerAsync(ownerAddress);
        }

        public async Task<bool> CanUserEditCampaign(int campaignId, string userAddress)
        {
            var campaign = await _repository.GetCampaignByIdAsync(campaignId);
            return campaign != null && campaign.OwnerAddress.ToLower() == userAddress.ToLower();
        }

        public async Task<bool> CanUserCreatePost(int campaignId, string userAddress)
        {
            var campaign = await _repository.GetCampaignByIdAsync(campaignId);
            if (campaign == null || campaign.OwnerAddress.ToLower() != userAddress.ToLower())
            {
                return false;
            }
            var existingPosts = await _repository.GetPostsByCampaignIdAsync(campaignId);
            bool hasExistingPosts = existingPosts.Any();
            if (!hasExistingPosts)
            {
                return true;
            }

            return campaign.ApprovalStatus == ApprovalStatus.Approved;
        }
        #endregion
    }
}