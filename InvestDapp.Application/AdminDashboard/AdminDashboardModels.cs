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
}
