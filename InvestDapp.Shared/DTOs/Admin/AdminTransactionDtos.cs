using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using InvestDapp.Shared.Enums;

namespace InvestDapp.Shared.DTOs.Admin
{
    public class AdminTransactionRecordDto
    {
        public int Id { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public int CampaignId { get; set; }
        public string? CampaignName { get; set; }
        public string InvestorAddress { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string AmountFormatted { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public string? TransactionHash { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TransactionReportSummaryDto
    {
        public decimal TotalInvestment { get; set; }
        public decimal TotalRefund { get; set; }
        public decimal NetAmount => TotalInvestment - TotalRefund;
        public int InvestmentCount { get; set; }
        public int RefundCount { get; set; }
        public int TotalTransactions => InvestmentCount + RefundCount;
    }

    public class TransactionReportResultDto
    {
        public TransactionReportSummaryDto Summary { get; set; } = new();
        public IReadOnlyList<AdminTransactionRecordDto> Transactions { get; set; } = Array.Empty<AdminTransactionRecordDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPrevious => PageNumber > 1;
        public bool HasNext => PageNumber < TotalPages;
        public TransactionChartDataDto ChartData { get; set; } = new();
    }

    public class TransactionTimelinePointDto
    {
        public DateTime Period { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal InvestmentTotal { get; set; }
        public decimal RefundTotal { get; set; }
        public decimal NetAmount => InvestmentTotal - RefundTotal;
    }

    public class TransactionCampaignSummaryDto
    {
        public int CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public decimal InvestmentTotal { get; set; }
        public decimal RefundTotal { get; set; }
        public decimal NetAmount => InvestmentTotal - RefundTotal;
    }

    public class TransactionChartDataDto
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TransactionGrouping Grouping { get; set; } = TransactionGrouping.Daily;
        public IReadOnlyList<TransactionTimelinePointDto> Timeline { get; set; } = Array.Empty<TransactionTimelinePointDto>();
        public IReadOnlyList<TransactionCampaignSummaryDto> TopCampaigns { get; set; } = Array.Empty<TransactionCampaignSummaryDto>();
    }
}
