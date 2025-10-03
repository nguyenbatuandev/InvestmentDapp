using InvestDapp.Application.AdminAnalytics;
using InvestDapp.Areas.admin.ViewModels;
using InvestDapp.Shared.Common.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace InvestDapp.Areas.admin.Controllers
{
    [Area("Admin")]
    [Route("admin/transactions")]
    [Authorize(Roles = "Admin")]
    public class TransactionsController : Controller
    {
        private readonly ITransactionReportService _transactionReportService;

        public TransactionsController(ITransactionReportService transactionReportService)
        {
            _transactionReportService = transactionReportService;
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
    }
}
