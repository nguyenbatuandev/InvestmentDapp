using System.Security.Claims;
using InvestDapp.Application.SupportService;
using InvestDapp.Areas.admin.ViewModels.Support;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Common.Respone;
using InvestDapp.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvestDapp.Shared.Security;

namespace InvestDapp.Areas.admin.Controllers
{
    [Area("Admin")]
    [Route("admin/support")]
    [Authorize(Policy = AuthorizationPolicies.RequireSupportAgent)] // FIXED: Allow SupportAgent, Admin, SuperAdmin
    public class SupportController : Controller
    {
        private readonly ISupportTicketService _supportTicketService;
        private readonly InvestDbContext _dbContext;
        private readonly ILogger<SupportController> _logger;

        public SupportController(ISupportTicketService supportTicketService, InvestDbContext dbContext, ILogger<SupportController> logger)
        {
            _supportTicketService = supportTicketService;
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("")]
        [HttpGet("index")]
        [HttpGet("tickets")]
        public async Task<IActionResult> Index(
            SupportTicketStatus? status,
            SupportTicketPriority? priority,
            SupportTicketSlaStatus? slaStatus,
            int page = 1,
            int pageSize = 20,
            string? keyword = null,
            string scope = "inbox",
            CancellationToken cancellationToken = default)
        {
            var viewModel = await BuildListViewModelAsync(status, priority, slaStatus, page, pageSize, keyword, scope, cancellationToken);
            ViewData["Title"] = "Quản lý ticket hỗ trợ";
            return View("~/Areas/admin/Views/Support/Index.cshtml", viewModel);
        }

        [HttpGet("list-fragment")]
        public async Task<IActionResult> ListFragment(
            SupportTicketStatus? status,
            SupportTicketPriority? priority,
            SupportTicketSlaStatus? slaStatus,
            int page = 1,
            int pageSize = 20,
            string? keyword = null,
            string scope = "inbox",
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("[SUPPORT FRAGMENT] Request received: scope={Scope}, status={Status}, page={Page}", scope, status, page);
                var viewModel = await BuildListViewModelAsync(status, priority, slaStatus, page, pageSize, keyword, scope, cancellationToken);
                _logger.LogInformation("[SUPPORT FRAGMENT] ViewModel built successfully, returning partial");
                return PartialView("~/Areas/admin/Views/Support/_TicketListPartial.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SUPPORT FRAGMENT] Error building fragment");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpGet("detail/{id:int}")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            return await RenderDetailAsync(id, null, null, cancellationToken);
        }

        [HttpPost("reply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(SupportTicketReplyForm form, [FromForm] IFormFileCollection? attachments, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return await RenderDetailAsync(form.TicketId, form, null, cancellationToken);
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                TempData["TicketError"] = "Không xác định được người dùng hiện tại.";
                return RedirectToAction(nameof(Details), new { id = form.TicketId });
            }

            var request = new AddSupportTicketMessageRequest
            {
                TicketId = form.TicketId,
                Message = form.Message,
                TransitionToCustomerWaiting = form.TransitionToCustomerWaiting
            };

            var files = attachments?.ToList() ?? new System.Collections.Generic.List<IFormFile>();
            var response = await _supportTicketService.AddMessageFromStaffAsync(request, currentUserId.Value, form.MarkAsResolved, files, cancellationToken);
            if (!response.Success)
            {
                ModelState.AddModelError(string.Empty, response.Message);
                TempData["TicketError"] = response.Message;
                return await RenderDetailAsync(form.TicketId, form, null, cancellationToken);
            }

            TempData["TicketSuccess"] = form.MarkAsResolved ? "Ticket đã được đánh dấu hoàn tất." : "Đã gửi phản hồi cho khách hàng.";
            return RedirectToAction(nameof(Details), new { id = form.TicketId });
        }

        [HttpPost("assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(SupportTicketAssignForm form, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return await RenderDetailAsync(form.TicketId, null, form, cancellationToken);
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                TempData["TicketError"] = "Không xác định được người dùng hiện tại.";
                return RedirectToAction(nameof(Details), new { id = form.TicketId });
            }

            var request = new AssignSupportTicketRequest
            {
                TicketId = form.TicketId,
                AssignedToUserId = form.AssignedToUserId,
                Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim()
            };

            var response = await _supportTicketService.AssignTicketAsync(request, currentUserId.Value, cancellationToken);
            if (!response.Success)
            {
                ModelState.AddModelError(string.Empty, response.Message);
                TempData["TicketError"] = response.Message;
                return await RenderDetailAsync(form.TicketId, null, form, cancellationToken);
            }

            TempData["TicketSuccess"] = "Ticket đã được giao cho nhân sự phụ trách.";
            return RedirectToAction(nameof(Details), new { id = form.TicketId });
        }

        [HttpPost("status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int ticketId, bool resolve = false, bool close = false, CancellationToken cancellationToken = default)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                TempData["TicketError"] = "Không xác định được người dùng hiện tại.";
                return RedirectToAction(nameof(Details), new { id = ticketId });
            }

            var response = await _supportTicketService.UpdateStatusAsync(ticketId, currentUserId.Value, resolve, close, cancellationToken);
            if (!response.Success)
            {
                TempData["TicketError"] = response.Message;
                return RedirectToAction(nameof(Details), new { id = ticketId });
            }

            if (close)
            {
                TempData["TicketSuccess"] = "Ticket đã được đóng.";
            }
            else if (resolve)
            {
                TempData["TicketSuccess"] = "Ticket đã được đánh dấu hoàn tất.";
            }
            else
            {
                TempData["TicketSuccess"] = "Cập nhật trạng thái thành công.";
            }

            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        private void ApplyScopeToFilter(string scope, SupportTicketFilterRequest filter)
        {
            switch (scope)
            {
                case "all":
                    break;
                case "unassigned":
                    filter.AssignedToUserId = -1;
                    break;
                case "mine":
                    var currentId = GetCurrentUserId();
                    if (currentId.HasValue)
                    {
                        filter.AssignedToUserId = currentId.Value;
                    }
                    break;
                case "sla-risk":
                    filter.SlaStatus = SupportTicketSlaStatus.AtRisk;
                    break;
                default:
                    filter.Status ??= SupportTicketStatus.New;
                    break;
            }
        }

        private async Task<SupportTicketListViewModel> BuildListViewModelAsync(
            SupportTicketStatus? status,
            SupportTicketPriority? priority,
            SupportTicketSlaStatus? slaStatus,
            int page,
            int pageSize,
            string? keyword,
            string scope,
            CancellationToken cancellationToken)
        {
            var sanitizedKeyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
            var filter = new SupportTicketFilterRequest
            {
                Status = status,
                Priority = priority,
                SlaStatus = slaStatus,
                Keyword = sanitizedKeyword,
                Page = page <= 0 ? 1 : page,
                PageSize = Math.Clamp(pageSize, 5, 100)
            };

            scope = string.IsNullOrWhiteSpace(scope) ? "inbox" : scope.Trim().ToLowerInvariant();
            ApplyScopeToFilter(scope, filter);

            var response = await _supportTicketService.GetTicketsForAdminAsync(filter, cancellationToken);
            if (!response.Success || response.Data == null)
            {
                TempData["TicketError"] = response.Message;
                response = new BaseResponse<SupportTicketListResult>
                {
                    Success = true,
                    Data = new SupportTicketListResult
                    {
                        Items = Array.Empty<SupportTicketSummaryResponse>(),
                        Total = 0,
                        Page = filter.Page,
                        PageSize = filter.PageSize
                    }
                };
            }

            var metrics = await BuildListMetricsAsync(cancellationToken);

            return new SupportTicketListViewModel
            {
                Filter = filter,
                Tickets = response.Data!.Items,
                TotalItems = response.Data.Total,
                Scope = scope,
                Metrics = metrics
            };
        }

        private async Task<SupportTicketListMetrics> BuildListMetricsAsync(CancellationToken cancellationToken)
        {
            var tickets = _dbContext.SupportTickets.AsNoTracking();
            var nowDate = DateTime.UtcNow.Date;
            var openStatuses = new[]
            {
                SupportTicketStatus.New,
                SupportTicketStatus.InProgress,
                SupportTicketStatus.WaitingForCustomer,
                SupportTicketStatus.Escalated
            };

            return new SupportTicketListMetrics
            {
                TotalOpen = await tickets.CountAsync(t => openStatuses.Contains(t.Status), cancellationToken),
                Unassigned = await tickets.CountAsync(t => t.AssignedToUserId == null && openStatuses.Contains(t.Status), cancellationToken),
                WaitingForCustomer = await tickets.CountAsync(t => t.Status == SupportTicketStatus.WaitingForCustomer, cancellationToken),
                SlaAtRisk = await tickets.CountAsync(t => t.SlaStatus == SupportTicketSlaStatus.AtRisk || t.SlaStatus == SupportTicketSlaStatus.Breached, cancellationToken),
                NewToday = await tickets.CountAsync(t => t.CreatedAt >= nowDate, cancellationToken)
            };
        }

        private async Task<IActionResult> RenderDetailAsync(int ticketId, SupportTicketReplyForm? replyForm, SupportTicketAssignForm? assignForm, CancellationToken cancellationToken)
        {
            var detailResponse = await _supportTicketService.GetTicketDetailForAdminAsync(ticketId, cancellationToken);
            if (!detailResponse.Success || detailResponse.Data == null)
            {
                TempData["TicketError"] = detailResponse.Message;
                return RedirectToAction(nameof(Index));
            }

            var staffResponse = await _supportTicketService.GetAssignableStaffAsync(cancellationToken);
            var staffOptions = staffResponse.Success && staffResponse.Data != null
                ? staffResponse.Data
                : Array.Empty<SupportStaffSummaryResponse>();

            var model = new SupportTicketDetailViewModel
            {
                Ticket = detailResponse.Data,
                StaffOptions = staffOptions,
                ReplyForm = replyForm ?? new SupportTicketReplyForm { TicketId = ticketId },
                AssignForm = assignForm ?? new SupportTicketAssignForm
                {
                    TicketId = ticketId,
                    AssignedToUserId = detailResponse.Data.AssignedToUserId ?? 0,
                    Notes = assignForm?.Notes
                }
            };

            ViewData["Title"] = $"Ticket {detailResponse.Data.TicketCode}";
            return View("~/Areas/admin/Views/Support/Details.cshtml", model);
        }

        private int? GetCurrentUserId()
        {
            var userIdValue = User?.FindFirst("UserId")?.Value
                ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value;

            if (int.TryParse(userIdValue, out var userId))
            {
                return userId;
            }

            return null;
        }
    }
}
