using System;
using System.Collections.Generic;
using InvestDapp.Shared.Enums;

namespace InvestDapp.ViewModels.Home
{
    public class HomeLandingViewModel
    {
        public LandingStatsViewModel Stats { get; set; } = new();
        public IReadOnlyList<CampaignSummaryCard> FeaturedCampaigns { get; set; } = Array.Empty<CampaignSummaryCard>();
        public IReadOnlyList<InvestmentTickerItem> RecentInvestments { get; set; } = Array.Empty<InvestmentTickerItem>();
        public IReadOnlyList<NewsSpotlightViewModel> Highlights { get; set; } = Array.Empty<NewsSpotlightViewModel>();
        public bool IsAuthenticated { get; set; }
        public string? WalletAddress { get; set; }
    }

    public class LandingStatsViewModel
    {
        public decimal TotalRaised { get; set; }
        public int TotalInvestors { get; set; }
        public int ActiveCampaigns { get; set; }
        public int CompletedCampaigns { get; set; }
        public decimal TotalValueLocked { get; set; }
    }

    public class CampaignSummaryCard
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public double GoalAmount { get; set; }
        public double RaisedAmount { get; set; }
        public double ProgressPercentage { get; set; }
        public CampaignStatus Status { get; set; }
        public bool IsHot { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class InvestmentTickerItem
    {
        public string CampaignName { get; set; } = string.Empty;
        public string InvestorAddress { get; set; } = string.Empty;
        public double Amount { get; set; }
        public DateTime Timestamp { get; set; }

        public string ShortInvestorAddress
        {
            get
            {
                if (string.IsNullOrWhiteSpace(InvestorAddress))
                {
                    return "--";
                }

                if (InvestorAddress.Length <= 12)
                {
                    return InvestorAddress;
                }

                return $"{InvestorAddress[..6]}…{InvestorAddress[^4..]}";
            }
        }
    }

    public class NewsSpotlightViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CampaignName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string Excerpt { get; set; } = string.Empty;
        public PostType PostType { get; set; }
        public DateTime PublishedAt { get; set; }
    }
}
