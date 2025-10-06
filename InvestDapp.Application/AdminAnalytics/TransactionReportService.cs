using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.DTOs.Admin;
using InvestDapp.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace InvestDapp.Application.AdminAnalytics
{
    public class TransactionReportService : ITransactionReportService
    {
        private readonly InvestDbContext _context;

        public TransactionReportService(InvestDbContext context)
        {
            _context = context;
        }

        public async Task<TransactionReportResultDto> GetTransactionsAsync(TransactionReportFilterRequest filterRequest)
        {
            filterRequest ??= new TransactionReportFilterRequest();

            var allRecords = await FetchRecordsAsync(filterRequest);

            var totalCount = allRecords.Count;
            var pageSize = filterRequest.IncludeAll
                ? (totalCount == 0 ? 1 : totalCount)
                : (filterRequest.PageSize <= 0 ? 10 : filterRequest.PageSize);

            var requestedPage = filterRequest.IncludeAll
                ? 1
                : (filterRequest.PageNumber <= 0 ? 1 : filterRequest.PageNumber);

            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);

            if (!filterRequest.IncludeAll)
            {
                requestedPage = Math.Min(Math.Max(requestedPage, 1), totalPages);
            }
            else
            {
                requestedPage = 1;
                totalPages = 1;
            }

            var pagedRecords = filterRequest.IncludeAll
                ? allRecords
                : allRecords
                    .Skip((requestedPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

            var summary = BuildSummary(allRecords);

            return new TransactionReportResultDto
            {
                Summary = summary,
                Transactions = pagedRecords,
                TotalCount = totalCount,
                PageNumber = requestedPage,
                PageSize = pageSize,
                TotalPages = totalPages,
                ChartData = BuildChartData(allRecords, TransactionGrouping.Daily)
            };
        }

        public async Task<IReadOnlyList<string>> GetCampaignNamesAsync()
        {
            return await _context.Campaigns
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => c.Name)
                .Distinct()
                .ToListAsync();
        }

        public async Task<TransactionChartDataDto> GetChartDataAsync(TransactionReportFilterRequest filterRequest, TransactionGrouping grouping, int topCampaigns = 5)
        {
            filterRequest ??= new TransactionReportFilterRequest();
            filterRequest.IncludeAll = true;

            var allRecords = await FetchRecordsAsync(filterRequest);
            return BuildChartData(allRecords, grouping, topCampaigns);
        }

        private async Task<List<AdminTransactionRecordDto>> FetchRecordsAsync(TransactionReportFilterRequest filterRequest)
        {
            var investmentQuery = _context.Investment
                .AsNoTracking()
                .Include(i => i.Campaign)
                .AsQueryable();

            var refundQuery = _context.Refunds
                .AsNoTracking()
                .Include(r => r.Campaign)
                .AsQueryable();

            var profitClaimQuery = _context.ProfitClaims
                .AsNoTracking()
                .Include(pc => pc.Profit)
                    .ThenInclude(p => p.Campaign)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterRequest.CampaignName))
            {
                var keyword = $"%{filterRequest.CampaignName.Trim()}%";
                investmentQuery = investmentQuery.Where(i => i.Campaign != null && EF.Functions.Like(i.Campaign.Name, keyword));
                refundQuery = refundQuery.Where(r => r.Campaign != null && EF.Functions.Like(r.Campaign.Name, keyword));
                profitClaimQuery = profitClaimQuery.Where(pc => pc.Profit != null && pc.Profit.Campaign != null && EF.Functions.Like(pc.Profit.Campaign.Name, keyword));
            }

            if (filterRequest.StartDate.HasValue)
            {
                var start = filterRequest.StartDate.Value.Date;
                investmentQuery = investmentQuery.Where(i => i.Timestamp >= start);
                refundQuery = refundQuery.Where(r => (r.ClaimedAt ?? DateTime.MinValue) >= start);
                profitClaimQuery = profitClaimQuery.Where(pc => pc.ClaimedAt >= start);
            }

            if (filterRequest.EndDate.HasValue)
            {
                var exclusiveEnd = filterRequest.EndDate.Value.Date.AddDays(1);
                investmentQuery = investmentQuery.Where(i => i.Timestamp < exclusiveEnd);
                refundQuery = refundQuery.Where(r => (r.ClaimedAt ?? DateTime.MinValue) < exclusiveEnd);
                profitClaimQuery = profitClaimQuery.Where(pc => pc.ClaimedAt < exclusiveEnd);
            }

            var records = new List<AdminTransactionRecordDto>();

            var includeInvestments = string.IsNullOrWhiteSpace(filterRequest.TransactionType) ||
                                     filterRequest.TransactionType.Equals("Investment", StringComparison.OrdinalIgnoreCase) ||
                                     filterRequest.TransactionType.Equals("Invest", StringComparison.OrdinalIgnoreCase);

            var includeRefunds = string.IsNullOrWhiteSpace(filterRequest.TransactionType) ||
                                 filterRequest.TransactionType.Equals("Refund", StringComparison.OrdinalIgnoreCase);

            var includeProfitClaims = string.IsNullOrWhiteSpace(filterRequest.TransactionType) ||
                                      filterRequest.TransactionType.Equals("Profit", StringComparison.OrdinalIgnoreCase) ||
                                      filterRequest.TransactionType.Equals("ClaimProfit", StringComparison.OrdinalIgnoreCase) ||
                                      filterRequest.TransactionType.Equals("ProfitClaim", StringComparison.OrdinalIgnoreCase) ||
                                      filterRequest.TransactionType.Equals("Claim Profit", StringComparison.OrdinalIgnoreCase);

            if (includeInvestments)
            {
                var investmentRecords = await investmentQuery
                    .Select(i => new AdminTransactionRecordDto
                    {
                        Id = i.Id,
                        TransactionType = "Investment",
                        CampaignId = i.CampaignId,
                        CampaignName = i.Campaign != null ? i.Campaign.Name : "Unknown Campaign",
                        InvestorAddress = i.InvestorAddress,
                        Amount = (decimal)i.Amount,
                        AmountFormatted = string.Empty,
                        OccurredAt = i.Timestamp,
                        TransactionHash = i.TransactionHash,
                        Status = "Completed"
                    })
                    .ToListAsync();

                foreach (var record in investmentRecords)
                {
                    record.AmountFormatted = BlockchainAmountConverter.FormatBnb(record.Amount);
                }

                records.AddRange(investmentRecords);
            }

            if (includeRefunds)
            {
                var refundRecords = await refundQuery
                    .Select(r => new
                    {
                        r.Id,
                        r.CampaignId,
                        CampaignName = r.Campaign != null ? r.Campaign.Name : "Unknown Campaign",
                        r.InvestorAddress,
                        r.AmountInWei,
                        r.ClaimedAt,
                        r.TransactionHash,
                        CampaignCreatedAt = r.Campaign != null ? (DateTime?)r.Campaign.CreatedAt : null
                    })
                    .ToListAsync();

                foreach (var refund in refundRecords)
                {
                    var amount = BlockchainAmountConverter.ToBnb(refund.AmountInWei);
                    var occurredAt = refund.ClaimedAt ?? refund.CampaignCreatedAt;

                    records.Add(new AdminTransactionRecordDto
                    {
                        Id = refund.Id,
                        TransactionType = "Refund",
                        CampaignId = refund.CampaignId,
                        CampaignName = refund.CampaignName,
                        InvestorAddress = refund.InvestorAddress,
                        Amount = amount,
                        AmountFormatted = BlockchainAmountConverter.FormatBnb(amount),
                        OccurredAt = occurredAt ?? DateTime.MinValue,
                        TransactionHash = refund.TransactionHash,
                        Status = refund.ClaimedAt.HasValue ? "Refunded" : "Pending"
                    });
                }
            }

            if (includeProfitClaims)
            {
                var profitClaimRecords = await profitClaimQuery
                    .Select(pc => new
                    {
                        pc.Id,
                        CampaignId = pc.Profit != null ? pc.Profit.CampaignId : 0,
                        CampaignName = pc.Profit != null && pc.Profit.Campaign != null ? pc.Profit.Campaign.Name : "Unknown Campaign",
                        pc.ClaimerWallet,
                        pc.Amount,
                        pc.ClaimedAt,
                        pc.TransactionHash
                    })
                    .ToListAsync();

                foreach (var claim in profitClaimRecords)
                {
                    records.Add(new AdminTransactionRecordDto
                    {
                        Id = claim.Id,
                        TransactionType = "Profit",
                        CampaignId = claim.CampaignId,
                        CampaignName = claim.CampaignName,
                        InvestorAddress = claim.ClaimerWallet,
                        Amount = claim.Amount,
                        AmountFormatted = BlockchainAmountConverter.FormatBnb(claim.Amount),
                        OccurredAt = claim.ClaimedAt,
                        TransactionHash = claim.TransactionHash,
                        Status = "Claimed"
                    });
                }
            }

            return records
                .OrderByDescending(r => r.OccurredAt == DateTime.MinValue ? DateTime.MinValue : r.OccurredAt)
                .ThenByDescending(r => r.Id)
                .ToList();
        }

        private static TransactionReportSummaryDto BuildSummary(IReadOnlyCollection<AdminTransactionRecordDto> records)
        {
            return new TransactionReportSummaryDto
            {
                TotalInvestment = records.Where(r => r.TransactionType.Equals("Investment", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount),
                TotalRefund = records.Where(r => r.TransactionType.Equals("Refund", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount),
                TotalProfit = records.Where(r => r.TransactionType.Equals("Profit", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount),
                InvestmentCount = records.Count(r => r.TransactionType.Equals("Investment", StringComparison.OrdinalIgnoreCase)),
                RefundCount = records.Count(r => r.TransactionType.Equals("Refund", StringComparison.OrdinalIgnoreCase)),
                ProfitCount = records.Count(r => r.TransactionType.Equals("Profit", StringComparison.OrdinalIgnoreCase))
            };
        }

        private static TransactionChartDataDto BuildChartData(IReadOnlyCollection<AdminTransactionRecordDto> records, TransactionGrouping grouping, int topCampaigns = 5)
        {
            if (records == null || records.Count == 0)
            {
                return new TransactionChartDataDto
                {
                    Grouping = grouping
                };
            }

            var timeline = records
                .Where(r => r.OccurredAt != DateTime.MinValue)
                .GroupBy(r => GetPeriodStart(r.OccurredAt, grouping))
                .OrderBy(g => g.Key)
                .Select(g => new TransactionTimelinePointDto
                {
                    Period = g.Key,
                    Label = FormatPeriodLabel(g.Key, grouping),
                    InvestmentTotal = g.Where(r => r.TransactionType.Equals("Investment", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount),
                    RefundTotal = g.Where(r => r.TransactionType.Equals("Refund", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount),
                    ProfitTotal = g.Where(r => r.TransactionType.Equals("Profit", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount)
                })
                .ToList();

            var campaigns = records
                .GroupBy(r => new { r.CampaignId, Name = r.CampaignName ?? "Không xác định" })
                .Select(g => new TransactionCampaignSummaryDto
                {
                    CampaignId = g.Key.CampaignId,
                    CampaignName = g.Key.Name,
                    InvestmentTotal = g.Where(r => r.TransactionType.Equals("Investment", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount),
                    RefundTotal = g.Where(r => r.TransactionType.Equals("Refund", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount),
                    ProfitTotal = g.Where(r => r.TransactionType.Equals("Profit", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount)
                })
                .OrderByDescending(c => c.InvestmentTotal)
                .ThenByDescending(c => c.NetAmount)
                .Take(Math.Max(topCampaigns, 1))
                .ToList();

            return new TransactionChartDataDto
            {
                Grouping = grouping,
                Timeline = timeline,
                TopCampaigns = campaigns
            };
        }

        private static DateTime GetPeriodStart(DateTime occurredAt, TransactionGrouping grouping)
        {
            var date = occurredAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc)
                : occurredAt;

            date = date.ToLocalTime().Date;

            return grouping switch
            {
                TransactionGrouping.Weekly => StartOfWeek(date),
                TransactionGrouping.Monthly => new DateTime(date.Year, date.Month, 1),
                _ => date
            };
        }

        private static string FormatPeriodLabel(DateTime periodStart, TransactionGrouping grouping)
        {
            return grouping switch
            {
                TransactionGrouping.Weekly => $"{periodStart:dd/MM} - {periodStart.AddDays(6):dd/MM}",
                TransactionGrouping.Monthly => periodStart.ToString("MM/yyyy"),
                _ => periodStart.ToString("dd/MM/yyyy")
            };
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff);
        }

        private static decimal ConvertWeiToBnb(string? amountInWei)
        {
            return BlockchainAmountConverter.ToBnb(amountInWei);
        }
    }
}
