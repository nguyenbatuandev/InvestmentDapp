using InvestDapp.Application.AdminAnalytics;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.DTOs.Admin;
using InvestDapp.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using InvestDapp.Shared.Security;

namespace InvestDapp.Areas.admin.Controllers
{
    [Area("Admin")]
    [Route("admin/api/transactions")]
    [Authorize(Policy = AuthorizationPolicies.RequireModerator)] // Match TransactionsController policy
    [ApiController]
    public class TransactionsApiController : ControllerBase
    {
        private readonly ITransactionReportService _transactionReportService;

        public TransactionsApiController(ITransactionReportService transactionReportService)
        {
            _transactionReportService = transactionReportService;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<TransactionReportSummaryDto>> GetSummary(DateTime? startDate, DateTime? endDate, string? transactionType, string? campaignName)
        {
            var filter = BuildFilter(startDate, endDate, transactionType, campaignName, includeAll: true);
            var result = await _transactionReportService.GetTransactionsAsync(filter);
            return Ok(result.Summary);
        }

        [HttpGet("timeline")]
        public async Task<ActionResult<TransactionChartDataDto>> GetTimeline(DateTime? startDate, DateTime? endDate, string? transactionType, string? campaignName, string grouping = "Daily", int top = 5)
        {
            var filter = BuildFilter(startDate, endDate, transactionType, campaignName, includeAll: true);
            var groupingEnum = grouping?.ToLowerInvariant() switch
            {
                "weekly" => TransactionGrouping.Weekly,
                "monthly" => TransactionGrouping.Monthly,
                _ => TransactionGrouping.Daily
            };

            top = top <= 0 ? 5 : top;
            var data = await _transactionReportService.GetChartDataAsync(filter, groupingEnum, top);
            return Ok(data);
        }

        [HttpGet("details")]
        public async Task<ActionResult<TransactionReportResultDto>> GetDetails(DateTime? startDate, DateTime? endDate, string? transactionType, string? campaignName, int page = 1, int pageSize = 100)
        {
            var filter = BuildFilter(startDate, endDate, transactionType, campaignName, includeAll: false);
            filter.PageNumber = page <= 0 ? 1 : page;
            filter.PageSize = pageSize <= 0 ? 100 : Math.Min(pageSize, 1000);

            var result = await _transactionReportService.GetTransactionsAsync(filter);
            return Ok(result);
        }

        private static TransactionReportFilterRequest BuildFilter(DateTime? startDate, DateTime? endDate, string? transactionType, string? campaignName, bool includeAll)
        {
            var sanitizedType = string.IsNullOrWhiteSpace(transactionType) ? null : transactionType.Trim();
            var sanitizedCampaign = string.IsNullOrWhiteSpace(campaignName) ? null : campaignName.Trim();

            return new TransactionReportFilterRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                TransactionType = sanitizedType,
                CampaignName = sanitizedCampaign,
                IncludeAll = includeAll
            };
        }
    }
}
