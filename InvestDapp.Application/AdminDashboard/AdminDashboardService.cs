using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Numerics;

namespace InvestDapp.Application.AdminDashboard
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly InvestDbContext _context;

        public AdminDashboardService(InvestDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardData> GetDashboardAsync(int months = 6, CancellationToken cancellationToken = default)
        {
            months = Math.Clamp(months, 1, 24);
            var nowUtc = DateTime.UtcNow;
            var trendStart = new DateTime(nowUtc.Year, nowUtc.Month, 1).AddMonths(-(months - 1));

            var summary = await BuildSummaryAsync(cancellationToken);
            var quickStats = await BuildQuickStatsAsync(nowUtc, cancellationToken);
            var campaigns = await GetRecentCampaignsAsync(cancellationToken);
            var activities = await GetRecentActivitiesAsync(cancellationToken);
            var investors = await GetTopInvestorsAsync(cancellationToken);
            var trend = await GetInvestmentTrendAsync(trendStart, months, cancellationToken);

            return new AdminDashboardData
            {
                Summary = summary,
                QuickStats = quickStats,
                RecentCampaigns = campaigns,
                RecentActivities = activities,
                TopInvestors = investors,
                InvestmentTrend = trend
            };
        }

        private async Task<DashboardSummary> BuildSummaryAsync(CancellationToken cancellationToken)
        {
            var campaignsQuery = _context.Campaigns.AsNoTracking();
            var investmentQuery = _context.Investment.AsNoTracking();
            var refundsQuery = _context.Refunds.AsNoTracking();

            var totalCampaigns = await campaignsQuery.CountAsync(cancellationToken);
            var activeCampaigns = await campaignsQuery.CountAsync(c => c.Status == CampaignStatus.Active, cancellationToken);
            var pendingApprovalCampaigns = await campaignsQuery.CountAsync(c => c.ApprovalStatus == ApprovalStatus.Pending, cancellationToken);
            var completedCampaigns = await campaignsQuery.CountAsync(c => c.Status == CampaignStatus.Completed, cancellationToken);
            var distinctInvestors = await investmentQuery.Select(i => i.InvestorAddress).Distinct().CountAsync(cancellationToken);
            var totalInvestment = await investmentQuery.SumAsync(i => (double?)i.Amount, cancellationToken);
            var refundAmountsWei = await refundsQuery.Select(r => r.AmountInWei).ToListAsync(cancellationToken);
            var completedRaisedData = await campaignsQuery
                .Where(c => c.Status == CampaignStatus.Completed)
                .Select(c => new
                {
                    c.TotalInvestmentsOnCompletion,
                    c.CurrentRaisedAmount
                })
                .ToListAsync(cancellationToken);

            var completedRaisedTotal = completedRaisedData.Sum(c =>
                c.TotalInvestmentsOnCompletion > 0 ? c.TotalInvestmentsOnCompletion : c.CurrentRaisedAmount);

            var refundTotal = ConvertWeiListToBnb(refundAmountsWei);
            var estimatedProfit = completedRaisedTotal > 0
                ? decimal.Round((decimal)completedRaisedTotal * 0.04m, 2, MidpointRounding.AwayFromZero)
                : 0m;

            return new DashboardSummary
            {
                TotalCampaigns = totalCampaigns,
                ActiveCampaigns = activeCampaigns,
                PendingApprovalCampaigns = pendingApprovalCampaigns,
                CompletedCampaigns = completedCampaigns,
                TotalInvestors = distinctInvestors,
                TotalInvestment = (decimal)(totalInvestment ?? 0d),
                TotalRefund = refundTotal,
                EstimatedProfit = estimatedProfit
            };
        }

        private async Task<DashboardQuickStats> BuildQuickStatsAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            var kycQuery = _context.FundraiserKyc.AsNoTracking();
            var campaignQuery = _context.Campaigns.AsNoTracking();
            var withdrawalQuery = _context.WalletWithdrawalRequests.AsNoTracking();
            var messageQuery = _context.Messagers.AsNoTracking();
            var userQuery = _context.Users.AsNoTracking();

            var pendingKycs = await kycQuery.CountAsync(k => k.IsApproved == null, cancellationToken);
            var campaignsEndingSoon = await campaignQuery.CountAsync(c => c.Status == CampaignStatus.Active && c.EndTime >= nowUtc && c.EndTime <= nowUtc.AddDays(7), cancellationToken);
            var pendingWithdrawalAmount = await withdrawalQuery.Where(w => w.Status == WithdrawalStatus.Pending).SumAsync(w => (decimal?)w.Amount, cancellationToken);
            var newMessages = await messageQuery.CountAsync(m => m.SentAt >= nowUtc.AddDays(-7), cancellationToken);
            var newUsers = await userQuery.CountAsync(u => u.CreatedAt >= nowUtc.AddDays(-30), cancellationToken);

            return new DashboardQuickStats
            {
                PendingKycs = pendingKycs,
                CampaignsEndingSoon = campaignsEndingSoon,
                PendingWithdrawalAmount = pendingWithdrawalAmount ?? 0m,
                NewMessages = newMessages,
                NewUsers = newUsers
            };
        }

        private async Task<IReadOnlyList<DashboardCampaignListItem>> GetRecentCampaignsAsync(CancellationToken cancellationToken)
        {
            var campaigns = await _context.Campaigns
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new DashboardCampaignListItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    OwnerAddress = c.OwnerAddress,
                    GoalAmount = c.GoalAmount,
                    RaisedAmount = c.Investments.Sum(i => i.Amount),
                    Status = c.Status.ToString(),
                    ApprovalStatus = c.ApprovalStatus.ToString(),
                    EndTime = c.EndTime,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync(cancellationToken);

            foreach (var campaign in campaigns)
            {
                campaign.ProgressPercentage = campaign.GoalAmount > 0
                    ? Math.Min(100d, Math.Round((campaign.RaisedAmount / campaign.GoalAmount) * 100d, 2))
                    : 0d;
            }

            return campaigns;
        }

        private async Task<IReadOnlyList<DashboardActivityItem>> GetRecentActivitiesAsync(CancellationToken cancellationToken)
        {
            var activities = new List<DashboardActivityItem>(15);

            var recentCampaignsRaw = await _context.Campaigns
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new
                {
                    c.Name,
                    c.OwnerAddress,
                    c.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var recentCampaigns = recentCampaignsRaw
                .Select(c => new DashboardActivityItem
                {
                    Title = "Chiến dịch mới",
                    Description = $"{c.Name} được tạo bởi {Shorten(c.OwnerAddress)}",
                    Icon = "rocket-outline",
                    Tone = "info",
                    OccurredAt = c.CreatedAt
                })
                .ToList();

            activities.AddRange(recentCampaigns);

            var recentInvestmentsRaw = await _context.Investment
                .AsNoTracking()
                .OrderByDescending(i => i.Timestamp)
                .Take(5)
                .Select(i => new
                {
                    i.InvestorAddress,
                    i.Amount,
                    i.Timestamp
                })
                .ToListAsync(cancellationToken);

            var recentInvestments = recentInvestmentsRaw
                .Select(i => new DashboardActivityItem
                {
                    Title = "Đầu tư mới",
                    Description = $"{Shorten(i.InvestorAddress)} đã đầu tư {i.Amount.ToString("N2", CultureInfo.InvariantCulture)} BNB",
                    Icon = "wallet-outline",
                    Tone = "success",
                    OccurredAt = i.Timestamp
                })
                .ToList();

            activities.AddRange(recentInvestments);

            var recentKycsRaw = await _context.FundraiserKyc
                .AsNoTracking()
                .OrderByDescending(k => k.SubmittedAt)
                .Take(5)
                .Select(k => new
                {
                    k.SubmittedAt,
                    k.AccountType,
                    k.IsApproved,
                    Wallet = k.User != null ? k.User.WalletAddress : null
                })
                .ToListAsync(cancellationToken);

            var recentKycs = recentKycsRaw
                .Select(k => new DashboardActivityItem
                {
                    Title = "Yêu cầu KYC",
                    Description = $"{Shorten(k.Wallet ?? string.Empty)} gửi hồ sơ {(k.AccountType ?? "").ToLowerInvariant()}",
                    Icon = "document-text-outline",
                    Tone = k.IsApproved == true ? "success" : k.IsApproved == false ? "danger" : "warning",
                    OccurredAt = k.SubmittedAt
                })
                .ToList();

            activities.AddRange(recentKycs);

            return activities
                .OrderByDescending(a => a.OccurredAt)
                .Take(9)
                .ToList();
        }

        private async Task<IReadOnlyList<TopInvestorItem>> GetTopInvestorsAsync(CancellationToken cancellationToken)
        {
            var topInvestors = await _context.Investment
                .AsNoTracking()
                .GroupBy(i => i.InvestorAddress)
                .Select(g => new
                {
                    Address = g.Key,
                    Total = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Total)
                .Take(5)
                .ToListAsync(cancellationToken);

            var addresses = topInvestors.Select(x => x.Address).ToList();

            var userNames = await _context.Users
                .AsNoTracking()
                .Where(u => addresses.Contains(u.WalletAddress))
                .Select(u => new { u.WalletAddress, u.Name })
                .ToListAsync(cancellationToken);

            var lookup = userNames.ToDictionary(u => u.WalletAddress, u => u.Name);

            return topInvestors
                .Select(x => new TopInvestorItem
                {
                    WalletAddress = x.Address,
                    DisplayName = lookup.TryGetValue(x.Address, out var name) && !string.IsNullOrWhiteSpace(name) ? name : null,
                    TotalInvestment = (decimal)x.Total
                })
                .ToList();
        }

        private async Task<InvestmentTrendData> GetInvestmentTrendAsync(DateTime startPeriod, int months, CancellationToken cancellationToken)
        {
            var investments = await _context.Investment
                .AsNoTracking()
                .Where(i => i.Timestamp >= startPeriod)
                .GroupBy(i => new { i.Timestamp.Year, i.Timestamp.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Sum(x => x.Amount)
                })
                .ToListAsync(cancellationToken);

            var refunds = await _context.Refunds
                .AsNoTracking()
                .Where(r => (r.ClaimedAt ?? (r.Campaign != null ? r.Campaign.CreatedAt : DateTime.UtcNow)) >= startPeriod)
                .Select(r => new
                {
                    OccurredAt = r.ClaimedAt ?? (r.Campaign != null ? r.Campaign.CreatedAt : DateTime.UtcNow),
                    r.AmountInWei
                })
                .ToListAsync(cancellationToken);

            var refundGroups = refunds
                .GroupBy(r => new { r.OccurredAt.Year, r.OccurredAt.Month })
                .ToDictionary(g => (g.Key.Year, g.Key.Month), g => ConvertWeiListToBnb(g.Select(x => x.AmountInWei).ToList()));

            var points = new List<InvestmentTrendPoint>(months);
            var cursor = new DateTime(startPeriod.Year, startPeriod.Month, 1);

            for (int i = 0; i < months; i++)
            {
                var label = cursor.ToString("MM/yyyy");
                var investmentTotal = investments
                    .Where(x => x.Year == cursor.Year && x.Month == cursor.Month)
                    .Select(x => (decimal)x.Total)
                    .FirstOrDefault();

                refundGroups.TryGetValue((cursor.Year, cursor.Month), out var refundTotal);

                points.Add(new InvestmentTrendPoint
                {
                    Label = label,
                    Period = cursor,
                    InvestmentTotal = investmentTotal,
                    RefundTotal = refundTotal
                });

                cursor = cursor.AddMonths(1);
            }

            return new InvestmentTrendData
            {
                Points = points,
                RangeLabel = $"{points.FirstOrDefault()?.Label ?? string.Empty} - {points.LastOrDefault()?.Label ?? string.Empty}"
            };
        }

        private static decimal ConvertWeiListToBnb(IReadOnlyCollection<string> amountsInWei)
        {
            if (amountsInWei == null || amountsInWei.Count == 0)
            {
                return 0m;
            }

            decimal total = 0m;
            foreach (var amount in amountsInWei)
            {
                if (string.IsNullOrWhiteSpace(amount))
                {
                    continue;
                }

                if (!BigInteger.TryParse(amount, out var weiValue))
                {
                    continue;
                }

                const decimal weiPerBnb = 1_000_000_000_000_000_000m;
                total += (decimal)weiValue / weiPerBnb;
            }

            return total;
        }

        private static string Shorten(string value, int prefix = 6, int suffix = 4)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "--";
            }

            if (value.Length <= prefix + suffix + 3)
            {
                return value;
            }

            return $"{value.Substring(0, prefix)}...{value.Substring(value.Length - suffix)}";
        }
    }
}
