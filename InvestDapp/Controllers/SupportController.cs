using InvestDapp.Application.SupportService;
using InvestDapp.Application.UserService;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Common.Respone;
using InvestDapp.Shared.Enums;
using InvestDapp.ViewModels.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InvestDapp.Controllers
{
    [Authorize]
    public class SupportController : Controller
    {
        private readonly ISupportTicketService _supportTicketService;
        private readonly IUserService _userService;
        private readonly ILogger<SupportController> _logger;

        private static readonly IReadOnlyList<string> AllowedAttachmentExtensions = new[] { ".png", ".jpg", ".jpeg", ".pdf", ".docx", ".xlsx", ".txt" };
        private const int MaxAttachmentSizeMb = 10;

        private static readonly IReadOnlyList<string> DefaultCategories = new List<string>
        {
            "Vấn đề nạp/rút",
            "Giao dịch",
            "Ví & số dư",
            "KYC & xác minh",
            "Chiến dịch đầu tư",
            "Tài khoản & đăng nhập",
            "Khác"
        };

        public SupportController(
            ISupportTicketService supportTicketService,
            IUserService userService,
            ILogger<SupportController> logger)
        {
            _supportTicketService = supportTicketService;
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(SupportTicketStatus? status, string? keyword, int page = 1, CancellationToken cancellationToken = default)
        {
            var userId = await _userService.GetCurrentUserId();
            var filter = new SupportTicketFilterRequest
            {
                Status = status,
                Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim(),
                Page = page <= 0 ? 1 : page,
                PageSize = 10
            };

            var response = await _supportTicketService.GetTicketsForUserAsync(userId, filter, cancellationToken);
            if (!response.Success || response.Data == null)
            {
                TempData["SupportError"] = string.IsNullOrWhiteSpace(response.Message)
                    ? "Không thể tải danh sách ticket lúc này."
                    : response.Message;

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

            var viewModel = new UserSupportTicketListViewModel
            {
                Tickets = response.Data!,
                Status = status,
                Keyword = keyword
            };

            ViewData["Title"] = "Ticket hỗ trợ của tôi";
            return View("~/Views/Support/Index.cshtml", viewModel);
        }

        [HttpGet]
        public IActionResult Create()
        {
            PopulateFormMetadata();
            return View(new CreateSupportTicketRequest());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSupportTicketRequest request, List<IFormFile> attachments)
        {
            PopulateFormMetadata();

            if (!ModelState.IsValid)
            {
                return View(request);
            }

            try
            {
                var userId = await _userService.GetCurrentUserId();
                var response = await _supportTicketService.CreateTicketAsync(request, userId, attachments, HttpContext.RequestAborted);

                if (!response.Success)
                {
                    ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(response.Message)
                        ? "Không thể gửi ticket lúc này. Vui lòng thử lại sau."
                        : response.Message);
                    return View(request);
                }

                TempData["SupportTicketSuccess"] = response.Data?.TicketCode ?? "";
                return RedirectToAction(nameof(Confirmation));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi người dùng gửi ticket hỗ trợ");
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi gửi ticket. Vui lòng thử lại sau.");
                return View(request);
            }
        }

        [HttpGet]
        public IActionResult Confirmation()
        {
            PopulateFormMetadata();
            var ticketCode = TempData["SupportTicketSuccess"] as string;
            if (string.IsNullOrWhiteSpace(ticketCode))
            {
                return RedirectToAction(nameof(Create));
            }

            ViewBag.TicketCode = ticketCode;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            var userId = await _userService.GetCurrentUserId();
            var response = await _supportTicketService.GetTicketDetailForUserAsync(id, userId, cancellationToken);
            if (!response.Success || response.Data == null)
            {
                TempData["SupportError"] = string.IsNullOrWhiteSpace(response.Message)
                    ? "Không tìm thấy ticket."
                    : response.Message;
                return RedirectToAction(nameof(Index));
            }

            var model = BuildDetailViewModel(response.Data);
            ViewData["Title"] = $"Ticket {response.Data.TicketCode}";
            return View("~/Views/Support/Details.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(UserSupportTicketReplyForm form, List<IFormFile> attachments, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return await RenderDetailAsync(form.TicketId, form, cancellationToken);
            }

            try
            {
                var userId = await _userService.GetCurrentUserId();
                var request = new AddSupportTicketMessageRequest
                {
                    TicketId = form.TicketId,
                    Message = form.Message
                };

                var response = await _supportTicketService.AddMessageFromUserAsync(request, userId, attachments, cancellationToken);
                if (!response.Success || response.Data == null)
                {
                    TempData["SupportError"] = string.IsNullOrWhiteSpace(response.Message)
                        ? "Không thể gửi phản hồi lúc này."
                        : response.Message;
                    return await RenderDetailAsync(form.TicketId, form, cancellationToken);
                }

                TempData["SupportSuccess"] = "Đã gửi phản hồi tới đội ngũ hỗ trợ.";
                return RedirectToAction(nameof(Details), new { id = form.TicketId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi user gửi phản hồi ticket {TicketId}", form.TicketId);
                TempData["SupportError"] = "Có lỗi xảy ra khi gửi phản hồi.";
                return await RenderDetailAsync(form.TicketId, form, cancellationToken);
            }
        }

        private void PopulateFormMetadata()
        {
            ViewBag.PriorityOptions = Enum.GetValues(typeof(SupportTicketPriority))
                .Cast<SupportTicketPriority>()
                .Select(priority => new SelectListItem
                {
                    Text = GetPriorityLabel(priority),
                    Value = priority.ToString()
                })
                .ToList();

            ViewBag.Categories = DefaultCategories;
            ViewBag.MaxAttachmentSizeMb = MaxAttachmentSizeMb;
            ViewBag.AllowedAttachmentExtensions = AllowedAttachmentExtensions;
        }

        private static string GetPriorityLabel(SupportTicketPriority priority) => priority switch
        {
            SupportTicketPriority.Low => "Thấp",
            SupportTicketPriority.Normal => "Bình thường",
            SupportTicketPriority.High => "Cao",
            SupportTicketPriority.Critical => "Khẩn cấp",
            _ => priority.ToString()
        };

        private async Task<IActionResult> RenderDetailAsync(int ticketId, UserSupportTicketReplyForm? form, CancellationToken cancellationToken)
        {
            var userId = await _userService.GetCurrentUserId();
            var detail = await _supportTicketService.GetTicketDetailForUserAsync(ticketId, userId, cancellationToken);
            if (!detail.Success || detail.Data == null)
            {
                TempData["SupportError"] = string.IsNullOrWhiteSpace(detail.Message)
                    ? "Không thể tải ticket." : detail.Message;
                return RedirectToAction(nameof(Index));
            }

            var model = BuildDetailViewModel(detail.Data, form);
            ViewData["Title"] = $"Ticket {detail.Data.TicketCode}";
            return View("~/Views/Support/Details.cshtml", model);
        }

        private UserSupportTicketDetailViewModel BuildDetailViewModel(SupportTicketDetailResponse ticket, UserSupportTicketReplyForm? replyForm = null)
        {
            return new UserSupportTicketDetailViewModel
            {
                Ticket = ticket,
                ReplyForm = replyForm ?? new UserSupportTicketReplyForm { TicketId = ticket.Id },
                AllowedExtensions = AllowedAttachmentExtensions,
                MaxAttachmentSizeMb = MaxAttachmentSizeMb
            };
        }
    }
}
