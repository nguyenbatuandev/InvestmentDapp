using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models.Kyc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;

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
            var riskInsights = await BuildRiskAndComplianceAsync(nowUtc, cancellationToken);

            return new AdminDashboardData
            {
                Summary = summary,
                QuickStats = quickStats,
                RecentCampaigns = campaigns,
                RecentActivities = activities,
                TopInvestors = investors,
                InvestmentTrend = trend,
                RiskInsights = riskInsights
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
            var supportQuery = _context.SupportTickets.AsNoTracking();

            var pendingKycs = await kycQuery.CountAsync(k => k.IsApproved == null, cancellationToken);
            var campaignsEndingSoon = await campaignQuery.CountAsync(c => c.Status == CampaignStatus.Active && c.EndTime >= nowUtc && c.EndTime <= nowUtc.AddDays(7), cancellationToken);
            var pendingWithdrawalAmount = await withdrawalQuery.Where(w => w.Status == WithdrawalStatus.Pending).SumAsync(w => (decimal?)w.Amount, cancellationToken);
            var newMessages = await messageQuery.CountAsync(m => m.SentAt >= nowUtc.AddDays(-7), cancellationToken);
            var newUsers = await userQuery.CountAsync(u => u.CreatedAt >= nowUtc.AddDays(-30), cancellationToken);
            var openStatuses = new[]
            {
                SupportTicketStatus.New,
                SupportTicketStatus.InProgress,
                SupportTicketStatus.WaitingForCustomer,
                SupportTicketStatus.Escalated
            };
            var unresolvedSupportTickets = await supportQuery.CountAsync(t => openStatuses.Contains(t.Status), cancellationToken);

            return new DashboardQuickStats
            {
                PendingKycs = pendingKycs,
                CampaignsEndingSoon = campaignsEndingSoon,
                PendingWithdrawalAmount = pendingWithdrawalAmount ?? 0m,
                NewMessages = newMessages,
                NewUsers = newUsers,
                UnresolvedSupportTickets = unresolvedSupportTickets
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

        private async Task<RiskAndComplianceData> BuildRiskAndComplianceAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            var sevenDaysAgo = nowUtc.AddDays(-7);
            var lastDay = nowUtc.AddDays(-1);
            var thirtyDaysAgo = nowUtc.AddDays(-30);

            var recentInvestments = await _context.Investment
                .AsNoTracking()
                .Where(i => i.Timestamp >= sevenDaysAgo)
                .Select(i => new { i.CampaignId, i.Amount, i.Timestamp })
                .ToListAsync(cancellationToken);

            var spikesRaw = recentInvestments
                .GroupBy(i => i.CampaignId)
                .Select(group =>
                {
                    var last24h = group.Where(x => x.Timestamp >= lastDay).ToList();
                    var previous = group.Where(x => x.Timestamp < lastDay).ToList();

                    var last24hSum = last24h.Sum(x => x.Amount);
                    var previousSum = previous.Sum(x => x.Amount);
                    var previousDays = Math.Max((lastDay - sevenDaysAgo).TotalDays, 1d);
                    var averagePrevious = previousSum / previousDays;
                    var spikeRatio = averagePrevious > 0 ? last24hSum / averagePrevious : (last24hSum > 0 ? double.PositiveInfinity : 0);
                    var lastInvestment = group.Max(x => x.Timestamp);

                    return new
                    {
                        CampaignId = group.Key,
                        Last24hAmount = last24hSum,
                        AverageDailyAmount = averagePrevious,
                        SpikeRatio = spikeRatio,
                        LastInvestmentAt = lastInvestment
                    };
                })
                .Where(x => x.Last24hAmount > 0 && (x.AverageDailyAmount == 0 && x.Last24hAmount > 0.5 || x.SpikeRatio >= 1.5))
                .OrderByDescending(x => x.SpikeRatio)
                .ThenByDescending(x => x.Last24hAmount)
                .Take(5)
                .ToList();

            var duplicateInvestments = await _context.Investment
                .AsNoTracking()
                .Where(i => i.Timestamp >= thirtyDaysAgo)
                .Select(i => new { i.InvestorAddress, i.CampaignId, i.Amount, i.Timestamp })
                .ToListAsync(cancellationToken);

            var duplicateWalletsRaw = duplicateInvestments
                .GroupBy(i => i.InvestorAddress)
                .Select(group =>
                {
                    var campaigns = group.Select(x => x.CampaignId).Distinct().ToList();
                    var totalAmount = group.Sum(x => x.Amount);
                    var lastInvestmentAt = group.Max(x => x.Timestamp);

                    return new
                    {
                        Wallet = group.Key,
                        CampaignIds = campaigns,
                        CampaignCount = campaigns.Count,
                        TotalAmount = totalAmount,
                        LastInvestmentAt = lastInvestmentAt
                    };
                })
                .Where(x => x.CampaignCount >= 3 || x.TotalAmount >= 5d)
                .OrderByDescending(x => x.CampaignCount)
                .ThenByDescending(x => x.TotalAmount)
                .Take(5)
                .ToList();

            var withdrawalAlertsRaw = await _context.WithdrawalRequests
                .AsNoTracking()
                .Where(w => w.CreatedAt >= sevenDaysAgo)
                .GroupBy(w => w.CampaignId)
                .Select(group => new
                {
                    CampaignId = group.Key,
                    Pending = group.Count(w => w.Status == WithdrawalStatus.Pending),
                    Total = group.Count(),
                    LastCreated = group.Max(w => w.CreatedAt)
                })
                .Where(x => x.Pending >= 3 || x.Total >= 5)
                .OrderByDescending(x => x.Pending)
                .ThenByDescending(x => x.Total)
                .Take(5)
                .ToListAsync(cancellationToken);

            var campaignIds = new HashSet<int>();
            foreach (var item in spikesRaw)
            {
                campaignIds.Add(item.CampaignId);
            }
            foreach (var item in duplicateWalletsRaw.SelectMany(x => x.CampaignIds))
            {
                campaignIds.Add(item);
            }
            foreach (var item in withdrawalAlertsRaw)
            {
                campaignIds.Add(item.CampaignId);
            }

            var campaignLookup = await _context.Campaigns
                .AsNoTracking()
                .Where(c => campaignIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

            var spikes = spikesRaw.Select(item => new TransactionSpikeAlert
            {
                CampaignId = item.CampaignId,
                CampaignName = campaignLookup.TryGetValue(item.CampaignId, out var name) ? name : $"Campaign #{item.CampaignId}",
                Last24hAmount = Math.Round(item.Last24hAmount, 2),
                AverageDailyAmount = Math.Round(item.AverageDailyAmount, 2),
                SpikeRatio = item.AverageDailyAmount == 0 ? double.PositiveInfinity : Math.Round(item.SpikeRatio, 2),
                LastInvestmentAt = item.LastInvestmentAt
            }).ToList();

            var duplicateWallets = duplicateWalletsRaw.Select(item => new DuplicateWalletAlert
            {
                WalletAddress = item.Wallet,
                CampaignCount = item.CampaignCount,
                TotalAmount = Math.Round(item.TotalAmount, 2),
                LastInvestmentAt = item.LastInvestmentAt,
                SampleCampaigns = item.CampaignIds
                    .Select(id => campaignLookup.TryGetValue(id, out var name) ? name : $"Campaign #{id}")
                    .Take(3)
                    .ToList()
            }).ToList();

            var withdrawalAlerts = withdrawalAlertsRaw.Select(item => new WithdrawalAlertItem
            {
                CampaignId = item.CampaignId,
                CampaignName = campaignLookup.TryGetValue(item.CampaignId, out var name) ? name : $"Campaign #{item.CampaignId}",
                PendingCount = item.Pending,
                TotalLast7Days = item.Total,
                LastRequestAt = item.LastCreated
            }).ToList();

            var kycs = await _context.FundraiserKyc
                .AsNoTracking()
                .Include(k => k.User)
                .Where(k => k.SubmittedAt >= nowUtc.AddMonths(-6))
                .ToListAsync(cancellationToken);

            var pendingKycs = kycs.Where(k => k.IsApproved == null).ToList();
            var approvedKycs = kycs.Where(k => k.IsApproved == true).ToList();
            var rejectedKycs = kycs.Where(k => k.IsApproved == false).ToList();

            var pendingCount = pendingKycs.Count;
            var averagePendingDays = pendingCount == 0
                ? 0d
                : Math.Round(pendingKycs.Average(k => (nowUtc - k.SubmittedAt).TotalDays), 1);

            var oldestPending = pendingKycs
                .OrderByDescending(k => nowUtc - k.SubmittedAt)
                .Take(5)
                .Select(k => new KycPendingItem
                {
                    Id = k.Id,
                    WalletAddress = k.User?.WalletAddress ?? string.Empty,
                    AccountType = k.AccountType,
                    SubmittedAt = k.SubmittedAt,
                    PendingDays = Math.Round((nowUtc - k.SubmittedAt).TotalDays, 1),
                    SuggestedReviewer = SuggestReviewer(k)
                })
                .ToList();

            var totalKycsConsidered = kycs.Count;
            var approvalRate = totalKycsConsidered == 0 ? 0d : Math.Round(approvedKycs.Count * 100d / totalKycsConsidered, 1);
            var rejectionRate = totalKycsConsidered == 0 ? 0d : Math.Round(rejectedKycs.Count * 100d / totalKycsConsidered, 1);

            var pendingByAccountType = pendingKycs
                .GroupBy(k => k.AccountType ?? "khác")
                .Select(group => new KycAccountTypeStat
                {
                    AccountType = group.Key,
                    PendingCount = group.Count(),
                    AveragePendingDays = Math.Round(group.Average(k => (nowUtc - k.SubmittedAt).TotalDays), 1)
                })
                .OrderByDescending(x => x.PendingCount)
                .ToList();

            var rejectionReasons = rejectedKycs
                .Select(DeriveRejectionReason)
                .GroupBy(reason => reason)
                .Select(group => new KycRejectionReasonStat
                {
                    Reason = group.Key,
                    Count = group.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList();

            return new RiskAndComplianceData
            {
                TransactionSpikes = spikes,
                DuplicateWallets = duplicateWallets,
                WithdrawalAlerts = withdrawalAlerts,
                KycBacklog = new KycBacklogData
                {
                    PendingCount = pendingCount,
                    AveragePendingDays = averagePendingDays,
                    OldestPending = oldestPending,
                    ApprovalRate = approvalRate,
                    RejectionRate = rejectionRate,
                    PendingByAccountType = pendingByAccountType,
                    RejectionReasons = rejectionReasons
                }
            };
        }

        private static string SuggestReviewer(FundraiserKyc kyc)
        {
            var reviewers = new[] { "Lan", "Minh", "Thảo", "Quang" };
            if (reviewers.Length == 0)
            {
                return string.Empty;
            }

            var seed = (kyc.User?.WalletAddress ?? kyc.AccountType ?? string.Empty).GetHashCode();
            var index = Math.Abs(seed) % reviewers.Length;
            return reviewers[index];
        }

        private static string DeriveRejectionReason(FundraiserKyc kyc)
        {
            if (kyc == null)
            {
                return "Khác";
            }

            if (string.Equals(kyc.AccountType, "company", StringComparison.OrdinalIgnoreCase))
            {
                if (kyc.CompanyInfo == null)
                {
                    return "Thiếu hồ sơ công ty";
                }

                if (string.IsNullOrWhiteSpace(kyc.CompanyInfo.BusinessLicensePdfPath))
                {
                    return "Thiếu giấy phép kinh doanh";
                }

                if (string.IsNullOrWhiteSpace(kyc.CompanyInfo.DirectorIdImagePath))
                {
                    return "Thiếu giấy tờ đại diện pháp lý";
                }
            }
            else
            {
                if (kyc.IndividualInfo == null)
                {
                    return "Thiếu thông tin cá nhân";
                }

                if (string.IsNullOrWhiteSpace(kyc.IndividualInfo.SelfieWithIdPath))
                {
                    return "Thiếu ảnh xác thực";
                }

                if (string.IsNullOrWhiteSpace(kyc.IndividualInfo.IdFrontImagePath))
                {
                    return "Thiếu ảnh giấy tờ tùy thân";
                }
            }

            if (string.IsNullOrWhiteSpace(kyc.ContactEmail))
            {
                return "Thông tin liên hệ không hợp lệ";
            }

            return "Khác";
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
                total += BlockchainAmountConverter.ToBnb(amount);
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
