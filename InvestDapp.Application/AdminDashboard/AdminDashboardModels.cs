using System;
using System.Collections.Generic;

namespace InvestDapp.Application.AdminDashboard
{
    public class AdminDashboardData
    {
        public DashboardSummary Summary { get; set; } = new();
        public DashboardQuickStats QuickStats { get; set; } = new();
        public IReadOnlyList<DashboardCampaignListItem> RecentCampaigns { get; set; } = Array.Empty<DashboardCampaignListItem>();
        public IReadOnlyList<DashboardActivityItem> RecentActivities { get; set; } = Array.Empty<DashboardActivityItem>();
        public IReadOnlyList<TopInvestorItem> TopInvestors { get; set; } = Array.Empty<TopInvestorItem>();
        public InvestmentTrendData InvestmentTrend { get; set; } = new();
        public RiskAndComplianceData RiskInsights { get; set; } = new();
    }

    public class DashboardSummary
    {
        public int TotalCampaigns { get; set; }
        public int ActiveCampaigns { get; set; }
        public int PendingApprovalCampaigns { get; set; }
        public int CompletedCampaigns { get; set; }
        public int TotalInvestors { get; set; }
        public decimal TotalInvestment { get; set; }
        public decimal TotalRefund { get; set; }
        public decimal NetInvestment => TotalInvestment - TotalRefund;
        public decimal EstimatedProfit { get; set; }
    }

    public class DashboardQuickStats
    {
        public int PendingKycs { get; set; }
        public int CampaignsEndingSoon { get; set; }
        public decimal PendingWithdrawalAmount { get; set; }
        public int NewMessages { get; set; }
        public int NewUsers { get; set; }
        public int UnresolvedSupportTickets { get; set; }
    }

    public class DashboardCampaignListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OwnerAddress { get; set; } = string.Empty;
        public double GoalAmount { get; set; }
        public double RaisedAmount { get; set; }
        public double ProgressPercentage { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public DateTime EndTime { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DashboardActivityItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "pulse-outline";
        public string Tone { get; set; } = "info";
        public DateTime OccurredAt { get; set; }
    }

    public class TopInvestorItem
    {
        public string WalletAddress { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public decimal TotalInvestment { get; set; }
    }

    public class InvestmentTrendData
    {
        public IReadOnlyList<InvestmentTrendPoint> Points { get; set; } = Array.Empty<InvestmentTrendPoint>();
        public string RangeLabel { get; set; } = string.Empty;
    }

    public class InvestmentTrendPoint
    {
        public string Label { get; set; } = string.Empty;
        public DateTime Period { get; set; }
        public decimal InvestmentTotal { get; set; }
        public decimal RefundTotal { get; set; }
    }

    public class RiskAndComplianceData
    {
        public IReadOnlyList<TransactionSpikeAlert> TransactionSpikes { get; set; } = Array.Empty<TransactionSpikeAlert>();
        public IReadOnlyList<DuplicateWalletAlert> DuplicateWallets { get; set; } = Array.Empty<DuplicateWalletAlert>();
        public IReadOnlyList<WithdrawalAlertItem> WithdrawalAlerts { get; set; } = Array.Empty<WithdrawalAlertItem>();
        public KycBacklogData KycBacklog { get; set; } = new();
    }

    public class TransactionSpikeAlert
    {
        public int CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public double Last24hAmount { get; set; }
        public double AverageDailyAmount { get; set; }
        public double SpikeRatio { get; set; }
        public DateTime LastInvestmentAt { get; set; }
    }

    public class DuplicateWalletAlert
    {
        public string WalletAddress { get; set; } = string.Empty;
        public int CampaignCount { get; set; }
        public double TotalAmount { get; set; }
        public DateTime LastInvestmentAt { get; set; }
        public IReadOnlyList<string> SampleCampaigns { get; set; } = Array.Empty<string>();
    }

    public class WithdrawalAlertItem
    {
        public int CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public int PendingCount { get; set; }
        public int TotalLast7Days { get; set; }
        public DateTime LastRequestAt { get; set; }
    }

    public class KycBacklogData
    {
        public int PendingCount { get; set; }
        public double AveragePendingDays { get; set; }
        public IReadOnlyList<KycPendingItem> OldestPending { get; set; } = Array.Empty<KycPendingItem>();
        public double ApprovalRate { get; set; }
        public double RejectionRate { get; set; }
        public IReadOnlyList<KycAccountTypeStat> PendingByAccountType { get; set; } = Array.Empty<KycAccountTypeStat>();
        public IReadOnlyList<KycRejectionReasonStat> RejectionReasons { get; set; } = Array.Empty<KycRejectionReasonStat>();
    }

    public class KycPendingItem
    {
        public int Id { get; set; }
        public string WalletAddress { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public double PendingDays { get; set; }
        public string SuggestedReviewer { get; set; } = string.Empty;
    }

    public class KycAccountTypeStat
    {
        public string AccountType { get; set; } = string.Empty;
        public int PendingCount { get; set; }
        public double AveragePendingDays { get; set; }
    }

    public class KycRejectionReasonStat
    {
        public string Reason { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
