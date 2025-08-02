using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Models;
using Microsoft.EntityFrameworkCore;


namespace InvestDapp.Infrastructure.Data.Repository
{
    public class CampaignRepository : ICampaign
    {
        private readonly InvestDbContext _context;
        public CampaignRepository(InvestDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Campaign>> GetAllCampaignsAsync()
        {
            var campaigns = await _context.Campaigns
                .Include(c => c.category)
                .Include(c => c.Investments)
                .Include(c => c.WithdrawalRequests)
                .Include(c => c.Profits)
                .Include(c => c.Refund) // Bao gồm Refund nếu cần
                .ToListAsync();
            return campaigns;
        }

        public async Task<Campaign?> GetCampaignByIdAsync(int? id)
        {
            var campaign = await _context.Campaigns
                .Include(c => c.category)
                .Include(c => c.Investments)
                .Include(c => c.WithdrawalRequests)
                .Include(c => c.Profits)
                .Include(c => c.Refund) // Bao gồm Refund nếu cần
                .FirstOrDefaultAsync(c => c.Id == id);
            return campaign;
        }

        public async Task<Campaign> UpdateCampaignStatusAsync(Campaign campaign)
        {
            var existing = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaign.Id);
            if (existing == null)
                throw new Exception("Campaign not found");

            // Cập nhật có điều kiện
            if (!string.IsNullOrWhiteSpace(campaign.ShortDescription))
                existing.ShortDescription = campaign.ShortDescription;

            if (!string.IsNullOrWhiteSpace(campaign.Description))
                existing.Description = campaign.Description;

            if (!string.IsNullOrWhiteSpace(campaign.ImageUrl))
                existing.ImageUrl = campaign.ImageUrl;

            if (campaign.categoryId.HasValue)
                existing.categoryId = campaign.categoryId;

            await _context.SaveChangesAsync();

            // Load lại kèm category (navigation property)
            var updated = await _context.Campaigns
                .Include(c => c.category)
                .Include(c => c.Investments)
                .Include(c => c.WithdrawalRequests)
                .Include(c => c.Profits)
                .Include(c => c.Refund)
                .FirstOrDefaultAsync(c => c.Id == campaign.Id);

            return updated!;
        }

        public async Task<Campaign> CreateCampaignAsync(Campaign campaign)
        {
            _context.Campaigns.Add(campaign);
            await _context.SaveChangesAsync();
            return campaign;
        }

        public async Task<IEnumerable<Campaign>> GetCampaignsByOwnerAsync(string ownerAddress)
        {
            return await _context.Campaigns
                .Include(c => c.category)
                .Include(c => c.Posts)
                .Where(c => c.OwnerAddress.ToLower() == ownerAddress.ToLower())
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
    }
}
