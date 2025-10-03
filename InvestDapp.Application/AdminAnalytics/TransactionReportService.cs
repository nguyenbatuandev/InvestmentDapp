using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.DTOs.Admin;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
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

            var pageSize = filterRequest.PageSize <= 0 ? 10 : filterRequest.PageSize;
            var requestedPage = filterRequest.PageNumber <= 0 ? 1 : filterRequest.PageNumber;

            var investmentQuery = _context.Investment
                .AsNoTracking()
                .Include(i => i.Campaign)
                .AsQueryable();

            var refundQuery = _context.Refunds
                .AsNoTracking()
                .Include(r => r.Campaign)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterRequest.CampaignName))
            {
                var keyword = $"%{filterRequest.CampaignName.Trim()}%";
                investmentQuery = investmentQuery.Where(i => i.Campaign != null && EF.Functions.Like(i.Campaign.Name, keyword));
                refundQuery = refundQuery.Where(r => r.Campaign != null && EF.Functions.Like(r.Campaign.Name, keyword));
            }

            if (filterRequest.StartDate.HasValue)
            {
                var start = filterRequest.StartDate.Value.Date;
                investmentQuery = investmentQuery.Where(i => i.Timestamp >= start);
                refundQuery = refundQuery.Where(r => (r.ClaimedAt ?? DateTime.MinValue) >= start);
            }

            if (filterRequest.EndDate.HasValue)
            {
                var exclusiveEnd = filterRequest.EndDate.Value.Date.AddDays(1);
                investmentQuery = investmentQuery.Where(i => i.Timestamp < exclusiveEnd);
                refundQuery = refundQuery.Where(r => (r.ClaimedAt ?? DateTime.MinValue) < exclusiveEnd);
            }

            var records = new List<AdminTransactionRecordDto>();

            var includeInvestments = string.IsNullOrWhiteSpace(filterRequest.TransactionType) ||
                                     filterRequest.TransactionType.Equals("Investment", StringComparison.OrdinalIgnoreCase) ||
                                     filterRequest.TransactionType.Equals("Invest", StringComparison.OrdinalIgnoreCase);

            var includeRefunds = string.IsNullOrWhiteSpace(filterRequest.TransactionType) ||
                                 filterRequest.TransactionType.Equals("Refund", StringComparison.OrdinalIgnoreCase);

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
                        AmountFormatted = i.Amount.ToString("N4", CultureInfo.InvariantCulture),
                        OccurredAt = i.Timestamp,
                        TransactionHash = i.TransactionHash,
                        Status = "Completed"
                    })
                    .ToListAsync();

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
                    var amount = ConvertWeiToBnb(refund.AmountInWei);
                    var occurredAt = refund.ClaimedAt ?? refund.CampaignCreatedAt;

                    records.Add(new AdminTransactionRecordDto
                    {
                        Id = refund.Id,
                        TransactionType = "Refund",
                        CampaignId = refund.CampaignId,
                        CampaignName = refund.CampaignName,
                        InvestorAddress = refund.InvestorAddress,
                        Amount = amount,
                        AmountFormatted = amount.ToString("N4", CultureInfo.InvariantCulture),
                        OccurredAt = occurredAt ?? DateTime.MinValue,
                        TransactionHash = refund.TransactionHash,
                        Status = refund.ClaimedAt.HasValue ? "Refunded" : "Pending"
                    });
                }
            }

            var orderedRecords = records
                .OrderByDescending(r => r.OccurredAt == DateTime.MinValue ? DateTime.MinValue : r.OccurredAt)
                .ThenByDescending(r => r.Id)
                .ToList();

            var totalCount = orderedRecords.Count;
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            var currentPage = Math.Min(Math.Max(requestedPage, 1), totalPages);
            var pagedRecords = orderedRecords
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var summary = new TransactionReportSummaryDto
            {
                TotalInvestment = orderedRecords.Where(r => r.TransactionType == "Investment").Sum(r => r.Amount),
                TotalRefund = orderedRecords.Where(r => r.TransactionType == "Refund").Sum(r => r.Amount),
                InvestmentCount = orderedRecords.Count(r => r.TransactionType == "Investment"),
                RefundCount = orderedRecords.Count(r => r.TransactionType == "Refund")
            };

            return new TransactionReportResultDto
            {
                Summary = summary,
                Transactions = pagedRecords,
                TotalCount = totalCount,
                PageNumber = currentPage,
                PageSize = pageSize,
                TotalPages = totalPages
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

        private static decimal ConvertWeiToBnb(string? amountInWei)
        {
            if (string.IsNullOrWhiteSpace(amountInWei))
            {
                return 0m;
            }

            if (!BigInteger.TryParse(amountInWei, out var weiValue))
            {
                return 0m;
            }

            const decimal weiPerBnb = 1000000000000000000m; // 1e18
            return (decimal)weiValue / weiPerBnb;
        }
    }
}
