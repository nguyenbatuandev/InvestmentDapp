using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Models;
using InvestDapp.Shared.DTOs;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


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
                .Include(c => c.Refunds) 
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
                .Include(c => c.Refunds)
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
                .Include(c => c.Refunds)
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
                .Include(c => c.Investments)
                .Include(c => c.Profits)
                .Include(c => c.Refunds)
                .Include(c => c.WithdrawalRequests)
                .Where(c => c.OwnerAddress.ToLower() == ownerAddress.ToLower())
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Campaign>> GetCampaignsForAdminAsync(CampaignStatus? status = null, ApprovalStatus? approvalStatus = null, int page = 1, int pageSize = 10)
        {
            var query = _context.Campaigns
                .Include(c => c.category)
                .Include(c => c.Posts)
                .Include(c => c.Investments)
                .Include(c => c.WithdrawalRequests)
                .AsQueryable();

            // Apply filters
            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            if (approvalStatus.HasValue)
            {
                query = query.Where(c => c.ApprovalStatus == approvalStatus.Value);
            }

            // Apply pagination and ordering
            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<WithdrawalRequest> CreatRerequestWithdrawalAsync(WithdrawalRequestDto withdrawalRequestDto)
        {
            var qr = new WithdrawalRequest
            {
                CampaignId = withdrawalRequestDto.CampaignId,
                txhash = withdrawalRequestDto.TxHash,
                Reason = withdrawalRequestDto.Reason,
                CreatedAt = DateTime.UtcNow,
                RequesterAddress = withdrawalRequestDto.address,
                Status = WithdrawalStatus.Pending,
                AgreeVotes = 0,
                DisagreeVotes = 0,
            };
            _context.WithdrawalRequests.Add(qr);
            await _context.SaveChangesAsync();
            return qr;
        }

        public async Task<Refund> ClaimRefundAsync(ClaimRefundDto refundDto)
        {
            if (string.IsNullOrEmpty(refundDto.InvestorAddress))
                throw new ArgumentException("InvestorAddress is required for ClaimRefund");

            var investorLower = refundDto.InvestorAddress.ToLower();
            var totalInvested = await _context.Investment
                .Where(i => i.CampaignId == refundDto.CampaignId && i.InvestorAddress.ToLower() == investorLower)
                .SumAsync(i => i.Amount);
                
            var refund = new Refund
            {
                CampaignId = refundDto.CampaignId,
                TransactionHash = refundDto.TransactionHash,
                InvestorAddress = refundDto.InvestorAddress,
                AmountInWei = totalInvested.ToString(),
                ClaimedAt = DateTime.UtcNow,
                RefundReason = "Campaign failed or user requested refund"
            };
            
            await _context.Refunds.AddAsync(refund);
            await _context.SaveChangesAsync();
            return refund;
        }
    }
}
