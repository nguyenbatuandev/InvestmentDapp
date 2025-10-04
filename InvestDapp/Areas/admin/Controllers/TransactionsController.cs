using InvestDapp.Application.AdminAnalytics;
using InvestDapp.Areas.admin.ViewModels;
using InvestDapp.Shared.Common.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using InvestDapp.Shared.Security;

namespace InvestDapp.Areas.admin.Controllers
{
    [Area("Admin")]
    [Route("admin/transactions")]
    [Authorize(Policy = AuthorizationPolicies.RequireModerator)] // Moderator can view financial reports
    public class TransactionsController : Controller
    {
        private readonly ITransactionReportService _transactionReportService;
        private readonly ITransactionReportPdfService _transactionReportPdfService;

        public TransactionsController(ITransactionReportService transactionReportService, ITransactionReportPdfService transactionReportPdfService)
        {
            _transactionReportService = transactionReportService;
            _transactionReportPdfService = transactionReportPdfService;
        }

        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index(int page = 1, DateTime? startDate = null, DateTime? endDate = null, string? transactionType = null, string? campaignName = null)
        {
            var sanitizedType = string.IsNullOrWhiteSpace(transactionType) ? null : transactionType.Trim();
            var sanitizedCampaign = string.IsNullOrWhiteSpace(campaignName) ? null : campaignName.Trim();

            var filter = new TransactionReportFilterRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                TransactionType = sanitizedType,
                CampaignName = sanitizedCampaign,
                PageNumber = page <= 0 ? 1 : page,
                PageSize = 10
            };

            var result = await _transactionReportService.GetTransactionsAsync(filter);
            var campaigns = await _transactionReportService.GetCampaignNamesAsync();

            var viewModel = new TransactionReportPageViewModel
            {
                Filter = filter,
                Result = result,
                CampaignOptions = campaigns
            };

            ViewData["Title"] = "Thống kê giao dịch";
            return View(viewModel);
        }

        [HttpGet("chart-data")]
        public async Task<IActionResult> GetChartData(DateTime? startDate, DateTime? endDate, string? transactionType, string? campaignName, string grouping = "Daily")
        {
            var sanitizedType = string.IsNullOrWhiteSpace(transactionType) ? null : transactionType.Trim();
            var sanitizedCampaign = string.IsNullOrWhiteSpace(campaignName) ? null : campaignName.Trim();

            var filter = new TransactionReportFilterRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                TransactionType = sanitizedType,
                CampaignName = sanitizedCampaign,
                IncludeAll = true
            };

            var groupingEnum = grouping?.ToLowerInvariant() switch
            {
                "weekly" => InvestDapp.Shared.Enums.TransactionGrouping.Weekly,
                "monthly" => InvestDapp.Shared.Enums.TransactionGrouping.Monthly,
                _ => InvestDapp.Shared.Enums.TransactionGrouping.Daily
            };

            var chartData = await _transactionReportService.GetChartDataAsync(filter, groupingEnum);
            return Ok(chartData);
        }

        [HttpGet("export-pdf")]
        public async Task<IActionResult> ExportPdf(DateTime? startDate, DateTime? endDate, string? transactionType, string? campaignName)
        {
            var sanitizedType = string.IsNullOrWhiteSpace(transactionType) ? null : transactionType.Trim();
            var sanitizedCampaign = string.IsNullOrWhiteSpace(campaignName) ? null : campaignName.Trim();

            var filter = new TransactionReportFilterRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                TransactionType = sanitizedType,
                CampaignName = sanitizedCampaign,
                IncludeAll = true
            };

            var pdfBytes = await _transactionReportPdfService.GenerateReportAsync(filter);
            var filename = $"transaction-report-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            return File(pdfBytes, "application/pdf", filename);
        }
    }
}
