using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using InvestDapp.Models;

namespace InvestDapp.Infrastructure.Data.Repository
{
    public class CampaignPostRepository : ICampaignPostRepository
    {
        private readonly InvestDbContext _context;

        public CampaignPostRepository(InvestDbContext context)
        {
            _context = context;
        }

        #region Campaign Post Methods

        public async Task<CampaignPost> CreatePostAsync(CampaignPost post)
        {
            post.CreatedAt = DateTime.UtcNow;
            _context.CampaignPosts.Add(post);
            await _context.SaveChangesAsync();
            return post;
        }

        public async Task<CampaignPost?> GetPostByIdAsync(int id)
        {
            return await _context.CampaignPosts
                .Include(p => p.Campaign)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<CampaignPost>> GetPostsByCampaignIdAsync(int campaignId)
        {
            return await _context.CampaignPosts
                .Where(p => p.CampaignId == campaignId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<CampaignPost>> GetPostsByStatusAsync(ApprovalStatus status)
        {
            return await _context.CampaignPosts
                .Include(p => p.Campaign)
                .Where(p => p.ApprovalStatus == status)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<CampaignPost>> GetPendingPostsAsync()
        {
            return await GetPostsByStatusAsync(ApprovalStatus.Pending);
        }

        public async Task<IEnumerable<CampaignPost>> GetApprovedPostsAsync(int skip = 0, int take = 10)
        {
            return await _context.CampaignPosts
                .Include(p => p.Campaign)
                .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
                .OrderByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> UpdatePostAsync(CampaignPost post)
        {
            post.UpdatedAt = DateTime.UtcNow;
            _context.CampaignPosts.Update(post);
            var result = await _context.SaveChangesAsync();
            return result > 0;
        }

        public async Task<bool> DeletePostAsync(int id)
        {
            var post = await _context.CampaignPosts.FindAsync(id);
            if (post == null) return false;

            _context.CampaignPosts.Remove(post);
            var result = await _context.SaveChangesAsync();
            return result > 0;
        }

        public async Task<bool> ApprovePostAsync(int id, string adminNotes, string approvedBy)
        {
            var post = await _context.CampaignPosts.FindAsync(id);
            if (post == null) return false;

            post.ApprovalStatus = ApprovalStatus.Approved;
            post.AdminNotes = adminNotes;
            post.ApprovedBy = approvedBy;
            post.ApprovedAt = DateTime.UtcNow;
            post.UpdatedAt = DateTime.UtcNow;

            var result = await _context.SaveChangesAsync();
            return result > 0;
        }

        public async Task<bool> RejectPostAsync(int id, string adminNotes, string approvedBy)
        {
            var post = await _context.CampaignPosts.FindAsync(id);
            if (post == null) return false;

            post.ApprovalStatus = ApprovalStatus.Rejected;
            post.AdminNotes = adminNotes;
            post.ApprovedBy = approvedBy;
            post.ApprovedAt = DateTime.UtcNow;
            post.UpdatedAt = DateTime.UtcNow;

            var result = await _context.SaveChangesAsync();
            return result > 0;
        }

        #endregion

        #region Campaign Methods

        public async Task<bool> ApproveCampaignAsync(int id, string adminNotes, string approvedBy)
        {
            var campaign = await _context.Campaigns.FindAsync(id);
            if (campaign == null) return false;

            campaign.ApprovalStatus = ApprovalStatus.Approved;
            campaign.AdminNotes = adminNotes;
            campaign.ApprovedBy = approvedBy;
            campaign.ApprovedAt = DateTime.UtcNow;

            var result = await _context.SaveChangesAsync();
            return result > 0;
        }

        public async Task<bool> RejectCampaignAsync(int id, string adminNotes, string approvedBy)
        {
            var campaign = await _context.Campaigns.FindAsync(id);
            if (campaign == null) return false;

            campaign.ApprovalStatus = ApprovalStatus.Rejected;
            campaign.AdminNotes = adminNotes;
            campaign.ApprovedBy = approvedBy;
            campaign.ApprovedAt = DateTime.UtcNow;

            var result = await _context.SaveChangesAsync();
            return result > 0;
        }

        public async Task<IEnumerable<Campaign>> GetPendingCampaignsAsync()
        {
            return await _context.Campaigns
                .Include(c => c.category)
                .Where(c => c.ApprovalStatus == ApprovalStatus.Pending)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Campaign>> GetApprovedCampaignsAsync()
        {
            return await _context.Campaigns
                .Include(c => c.category)
                .Include(c => c.Posts)
                .Include(c => c.Investments)
                .Where(c => c.ApprovalStatus == ApprovalStatus.Approved)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Campaign?> GetCampaignByIdAsync(int id)
        {
            return await _context.Campaigns
                .Include(c => c.category)
                .Include(c => c.Posts)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<bool> UpdateCampaignAsync(Campaign campaign)
        {
            _context.Campaigns.Update(campaign);
            var result = await _context.SaveChangesAsync();
            return result > 0;
        }

        #endregion
    }
}