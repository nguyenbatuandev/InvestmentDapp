using InvestDapp.Application.CampaignService;
using InvestDapp.Application.MessageService;
using InvestDapp.Application.NotificationService;
using InvestDapp.Application.UserService;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using InvestDapp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;


namespace InvestDapp.Controllers
{
    [Authorize]
    public class CampaignsController : Controller
    {
        private readonly ICampaignPostService _campaignPostService;
        private readonly IUserService _userService;
        private readonly IConversationService _conversationService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly INotificationService _notificationService;
        private readonly ICampaign _campain;

        public CampaignsController(
            ICampaignPostService campaignPostService,
            IUserService userService,
            IConversationService conversationService,
            IServiceScopeFactory serviceScopeFactory,
            INotificationService notificationService,
            ICampaign campaign)
        {
            _campaignPostService = campaignPostService;
            _userService = userService;
            _conversationService = conversationService;
            _serviceScopeFactory = serviceScopeFactory;
            _notificationService = notificationService;
            _campain = campaign;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Lấy các campaign đã được approve
                var approvedCampaigns = await _campaignPostService.GetApprovedCampaignsAsync();
                return View(approvedCampaigns);
            }
            catch (Exception ex)
            {
                // Nếu có lỗi, trả về view với danh sách rỗng
                return View(new List<InvestDapp.Models.Campaign>());
            }
        }

        // GET: /Campaign/Create
        [Authorize(Roles = "KycVerified,Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // GET: /Campaign/MyCampaigns
        [Authorize(Roles = "KycVerified,Admin")]
        public async Task<IActionResult> MyCampaigns()
        {
            try
            {
                var wallet = User.FindFirst("WalletAddress")?.Value;
                if (string.IsNullOrEmpty(wallet))
                {
                    return RedirectToAction("Index", "Home");
                }

                var campaigns = await _campaignPostService.GetUserCampaignsAsync(wallet);
                return View(campaigns);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: /Campaign/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var campaign = await _campaignPostService.GetCampaignByIdAsync(id);
            if (campaign == null)
            {
                return NotFound();
            }

            return View(campaign);
        }

        // GET: /Campaign/Dashboard/5 - Trang dashboard cho dự án cụ thể
        [AllowAnonymous]
        public async Task<IActionResult> Dashboard(int id)
        {
            var campaign = await _campaignPostService.GetCampaignByIdAsync(id);
            if (campaign == null)
            {
                return NotFound();
            }

            return View(campaign);
        }

        // GET: /Campaign/WithdrawalRequests/5 - Trang chi tiết withdrawal requests
        [AllowAnonymous]
        public async Task<IActionResult> WithdrawalRequests(int id)
        {
            var campaign = await _campaignPostService.GetCampaignByIdAsync(id);
            if (campaign == null)
            {
                return NotFound();
            }

            return View(campaign);
        }


        #region Campaign API

        // POST: /Campaign/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "KycVerified,Admin")]
        public async Task<IActionResult> Create(CreateCampaignRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            try
            {
                var wallet = User.FindFirst("WalletAddress")?.Value;
                if (string.IsNullOrEmpty(wallet))
                {
                    ModelState.AddModelError("", "Không thể xác thực địa chỉ ví của bạn.");
                    return View(request);
                }

                // Validate EndTime - phải sau ít nhất 1 ngày từ bây giờ
                if (request.EndTime <= DateTime.UtcNow.AddDays(1))
                {
                    ModelState.AddModelError("EndTime", "Thời gian kết thúc phải sau ít nhất 1 ngày từ bây giờ.");
                    return View(request);
                }

                // Validate ImageUrl if provided
                if (!string.IsNullOrEmpty(request.ImageUrl))
                {
                    if (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        ModelState.AddModelError("ImageUrl", "URL hình ảnh không hợp lệ. Vui lòng nhập URL đầy đủ bắt đầu bằng http:// hoặc https://");
                        return View(request);
                    }
                }

                var campaign = await _campaignPostService.CreateCampaignAsync(request, wallet);

                if (campaign == null)
                {
                    ModelState.AddModelError("", "Không thể tạo chiến dịch. Vui lòng thử lại.");
                    return View(request);
                }

                TempData["SuccessMessage"] = "Chiến dịch đã được tạo thành công!";

                return RedirectToAction("CreatePost", new { campaignId = campaign.Id });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;

                // Check for specific database errors
                if (innerMessage.Contains("duplicate key") || innerMessage.Contains("UNIQUE constraint"))
                {
                    ModelState.AddModelError("", "Đã tồn tại chiến dịch với thông tin tương tự. Vui lòng kiểm tra lại.");
                }
                else if (innerMessage.Contains("foreign key") || innerMessage.Contains("REFERENCE constraint"))
                {
                    ModelState.AddModelError("", "Có lỗi liên quan đến dữ liệu tham chiếu. Vui lòng thử lại.");
                }
                else
                {
                    ModelState.AddModelError("", $"Lỗi cơ sở dữ liệu: {innerMessage}");
                }
                return View(request);
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                ModelState.AddModelError("", "Lỗi kết nối cơ sở dữ liệu. Vui lòng thử lại sau.");
                return View(request);
            }
            catch (Exception ex)
            {
                var detailedError = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError("", $"Đã xảy ra lỗi không mong muốn: {detailedError}");
                return View(request);
            }
        }

        #endregion

        #region Campaign Posts Views

        // GET: /Campaign/CreatePost/5
        [Authorize(Roles = "KycVerified,Admin")]
        public async Task<IActionResult> CreatePost(int campaignId)
        {
            var campaign = await _campaignPostService.GetCampaignByIdAsync(campaignId);
            if (campaign == null)
            {
                return NotFound();
            }

            var wallet = User.FindFirst("WalletAddress")?.Value;
            if (!await _campaignPostService.CanUserCreatePost(campaignId, wallet))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền tạo bài viết cho chiến dịch này.";
                return RedirectToAction("Details", new { id = campaignId });
            }

            // Removed automatic notification here to avoid duplicate notifications.
            // Notifications are sent when a post is actually created (POST CreatePost).

            ViewBag.Campaign = campaign;
            var model = new CreateCampaignPostRequest { CampaignId = campaignId };
            return View(model);
        }

        // GET: /Campaign/Posts/5
        public async Task<IActionResult> Posts(int campaignId)
        {
            var campaign = await _campaignPostService.GetCampaignByIdAsync(campaignId);
            if (campaign == null)
            {
                return NotFound();
            }

            var posts = await _campaignPostService.GetPostsByCampaignIdAsync(campaignId);
            ViewBag.Campaign = campaign;
            return View(posts);
        }

        // GET: /Campaign/PostDetails/5
        public async Task<IActionResult> PostDetails(int id)
        {
            var post = await _campaignPostService.GetPostByIdAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            // Only show approved posts to public, or show to admin/owner
            var wallet = User.FindFirst("WalletAddress")?.Value;
            bool isAdmin = User.IsInRole("Admin");
            bool isOwner = post.AuthorAddress == wallet;

            if (post.ApprovalStatus != InvestDapp.Shared.Enums.ApprovalStatus.Approved && !isAdmin && !isOwner)
            {
                TempData["ErrorMessage"] = "Bài viết này chưa được phê duyệt hoặc bạn không có quyền xem.";
                return RedirectToAction("Index", "Home");
            }

            return View(post);
        }

        #endregion

        #region Campaign Posts API

        // POST: /Campaign/CreatePost
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "KycVerified,Admin")]
        public async Task<IActionResult> CreatePost(CreateCampaignPostRequest request)
        {
            if (!ModelState.IsValid)
            {
                var campaign = await _campaignPostService.GetCampaignByIdAsync(request.CampaignId);
                ViewBag.Campaign = campaign;
                return View(request);
            }

            try
            {
                var wallet = User.FindFirst("WalletAddress")?.Value;
                if (string.IsNullOrEmpty(wallet))
                {
                    ModelState.AddModelError("", "Không thể xác thực địa chỉ ví của bạn.");
                    return View(request);
                }

                var post = await _campaignPostService.CreatePostAsync(request, wallet);

                // Lấy thông tin user để có UserId
                var user = await _userService.GetUserByWalletAddressAsync(wallet);
                if (user?.Data != null)
                {
                    var postUrl = Url.Action("PostDetails", "Campaigns", new { id = post.Id }, Request.Scheme);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                            // create per-investor notifications and send realtime events
                            var notyReq = new CreateNotificationToCampaignRequest
                            {
                                CampaignId = request.CampaignId,
                                Title = $"Bài viết mới: {post.Title}",
                                Message = $"Một bài viết mới đã được đăng: {post.Title}",
                                Type = "NewPost",
                                Data = postUrl
                            };

                            await notificationService.CreateNotificationForCampaignInvestorsAsync(notyReq);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error sending post notification: {ex.Message}");
                        }
                    });
                }

                var campaign = await _campaignPostService.GetCampaignByIdAsync(request.CampaignId);
                var allPosts = await _campaignPostService.GetPostsByCampaignIdAsync(request.CampaignId);
                bool isFirstPost = allPosts.Count() == 1;

                if (isFirstPost)
                {
                    TempData["SuccessMessage"] = "Bài viết đầu tiên đã được tạo và tự động phê duyệt! Chiến dịch hiện đang chờ admin duyệt. Thông báo đã được gửi vào group chat.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Bài viết đã được tạo thành công và đang chờ admin duyệt. Thông báo đã được gửi vào group chat.";
                }

                return RedirectToAction("AwaitingApproval", new { campaignId = request.CampaignId });
            }
            catch (UnauthorizedAccessException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Details", new { id = request.CampaignId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Đã xảy ra lỗi khi tạo bài viết. Vui lòng thử lại.");
                var campaign = await _campaignPostService.GetCampaignByIdAsync(request.CampaignId);
                ViewBag.Campaign = campaign;
                return View(request);
            }
        }

        // POST: /Campaign/DeletePost/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "KycVerified,Admin")]
        public async Task<IActionResult> DeletePost(int id, string? returnUrl = null)
        {
            try
            {
                var wallet = User.FindFirst("WalletAddress")?.Value;
                var post = await _campaignPostService.GetPostByIdAsync(id);

                if (post == null)
                {
                    if (Request.Headers["Content-Type"].ToString().Contains("application/json") ||
                        Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Không tìm thấy bài viết." });
                    }
                    return NotFound();
                }

                await _campaignPostService.DeletePostAsync(id, wallet);

                if (Request.Headers["Content-Type"].ToString().Contains("application/json") ||
                    Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    return Json(new { success = true, message = "Bài viết đã được xóa thành công." });
                }

                TempData["SuccessMessage"] = "Bài viết đã được xóa thành công.";

                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Posts", new { campaignId = post.CampaignId });
            }
            catch (UnauthorizedAccessException ex)
            {
                if (Request.Headers["Content-Type"].ToString().Contains("application/json") ||
                    Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    return Json(new { success = false, message = ex.Message });
                }
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", "Home");
            }
            catch (Exception)
            {
                if (Request.Headers["Content-Type"].ToString().Contains("application/json") ||
                    Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    Request.ContentType?.Contains("multipart/form-data") == true)
                {
                    return Json(new { success = false, message = "Đã xảy ra lỗi khi xóa bài viết." });
                }
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi xóa bài viết.";
                return RedirectToAction("Index", "Home");
            }
        }

        #endregion

        #region Workflow Views

        // GET: /Campaign/AwaitingApproval/5
        [Authorize(Roles = "KycVerified,Admin")]
        public async Task<IActionResult> AwaitingApproval(int campaignId)
        {
            var campaign = await _campaignPostService.GetCampaignByIdAsync(campaignId);
            if (campaign == null)
            {
                return NotFound();
            }

            var wallet = User.FindFirst("WalletAddress")?.Value;
            if (!await _campaignPostService.CanUserEditCampaign(campaignId, wallet))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem thông tin này.";
                return RedirectToAction("Index", "Home");
            }

            var posts = await _campaignPostService.GetPostsByCampaignIdAsync(campaignId);
            ViewBag.Campaign = campaign;
            ViewBag.Posts = posts;
            return View();
        }

        #endregion
        // GET: /Campaign/Browse
        [AllowAnonymous]
        public async Task<IActionResult> Browse(int page = 1)
        {
            const int pageSize = 9;
            var posts = await _campaignPostService.GetApprovedPostsAsync(page, pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.HasMorePages = posts.Count() == pageSize;

            return View(posts);
        }

        // API: Get latest investments for real-time updates
        [HttpGet("api/campaigns/{id}/investments/latest")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLatestInvestments(int id, DateTime? since = null)
        {
            try
            {
                var campaign = await _campaignPostService.GetCampaignByIdAsync(id);
                if (campaign == null)
                    return NotFound();

                var cutoffTime = since ?? DateTime.UtcNow.AddMinutes(-5); // Default: last 5 minutes

                if (campaign.Investments == null)
                {
                    return Json(new List<object>());
                }

                var latestInvestments = campaign.Investments
                    .Where(i => i.Timestamp > cutoffTime)
                    .OrderByDescending(i => i.Timestamp)
                    .Select(i => new
                    {
                        InvestorAddress = i.InvestorAddress,
                        Amount = i.Amount,
                        Timestamp = i.Timestamp,
                        TransactionHash = i.TransactionHash
                    })
                    .ToList();

                return Json(latestInvestments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: /api/campaigns/{id}/summary - returns totals + latest investments for realtime UI
        [HttpGet("api/campaigns/{id}/summary")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCampaignSummary(int id, DateTime? since = null)
        {
            try
            {
                var campaign = await _campaignPostService.GetCampaignByIdAsync(id);
                if (campaign == null) return NotFound();

                var cutoffTime = since ?? DateTime.UtcNow.AddMinutes(-5);

                List<object> latestInvestments;
                if (campaign.Investments == null)
                {
                    latestInvestments = new List<object>();
                }
                else
                {
                    latestInvestments = campaign.Investments
                        .Where(i => i.Timestamp > cutoffTime)
                        .OrderByDescending(i => i.Timestamp)
                        .Select(i => (object)new
                        {
                            InvestorAddress = i.InvestorAddress,
                            Amount = i.Amount,
                            Timestamp = i.Timestamp,
                            TransactionHash = i.TransactionHash
                        })
                        .ToList();
                }

                var summary = new
                {
                    currentRaised = campaign.CurrentRaisedAmount,
                    investorCount = campaign.InvestorCount,
                    latest = latestInvestments
                };

                return Json(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: /api/campaigns/{id}/pending-withdrawal - returns the latest pending withdrawal request id for a campaign
        [HttpGet("api/campaigns/{id}/pending-withdrawal")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPendingWithdrawal(int id)
        {
            try
            {
                var campaign = await _campaignPostService.GetCampaignByIdAsync(id);
                if (campaign == null) return NotFound(new { error = "Không tìm thấy chiến dịch" });

                var pending = campaign.WithdrawalRequests?
                    .Where(w => w.Status == InvestDapp.Shared.Enums.WithdrawalStatus.Pending)
                    .OrderByDescending(w => w.CreatedAt)
                    .FirstOrDefault();

                if (pending != null)
                {
                    return Ok(new
                    {
                        requestId = pending.Id,
                        createdAt = pending.CreatedAt,
                        source = "db"
                    });
                }

                // Fallback: try to resolve requestId from on-chain events stored in EventLogBlockchain
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<InvestDbContext>();

                    var ev = await db.EventLogBlockchain
                        .Where(e => e.CampaignId == id && e.EventType == "WithdrawalRequested")
                        .OrderByDescending(e => e.BlockNumber)
                        .FirstOrDefaultAsync();

                    if (ev != null && !string.IsNullOrWhiteSpace(ev.EventData))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(ev.EventData);
                            var root = doc.RootElement;

                            JsonElement prop;
                            if (root.TryGetProperty("RequestId", out prop) || root.TryGetProperty("requestId", out prop))
                            {
                                int reqId;
                                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out reqId))
                                {
                                    return Ok(new { requestId = reqId, createdAt = ev.ProcessedAt, source = "onchain-event" });
                                }

                                // sometimes numbers are serialized as strings
                                var sval = prop.GetRawText().Trim('"');
                                if (int.TryParse(sval, out reqId))
                                {
                                    return Ok(new { requestId = reqId, createdAt = ev.ProcessedAt, source = "onchain-event" });
                                }
                            }
                        }
                        catch
                        {
                            // ignore parse errors and continue to return DB pending or NotFound below
                        }
                    }
                }

                // If no pending found at all, return not found
                if (pending == null)
                {
                    return NotFound(new { error = "Không tìm thấy yêu cầu rút vốn đang chờ xử lý" });
                }

                return NotFound(new { error = "Không tìm thấy yêu cầu rút vốn đang chờ xử lý" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST: /Campaigns/RequestFullWithdrawal - Record withdrawal request transaction
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestFullWithdrawal([FromBody] WithdrawalRequestDto request)
        {
            try
            {
                // Validate input
                if (request == null || request.CampaignId <= 0 || string.IsNullOrEmpty(request.TxHash) || string.IsNullOrEmpty(request.Reason))
                {
                    return BadRequest(new { error = "Dữ liệu không hợp lệ - thiếu thông tin bắt buộc" });
                }

                // Get current user wallet
                var userWallet = User.FindFirst("WalletAddress")?.Value;
                if (string.IsNullOrEmpty(userWallet))
                {
                    return Unauthorized(new { error = "Không tìm thấy địa chỉ ví" });
                }

                // Override with provided address if available (for security, ensure it matches logged in user)
                if (!string.IsNullOrEmpty(request.address) &&
                    !string.Equals(userWallet, request.address, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { error = "Địa chỉ ví không khớp với tài khoản đăng nhập" });
                }

                // Verify campaign ownership
                var campaign = await _campaignPostService.GetCampaignByIdAsync(request.CampaignId);
                if (campaign == null)
                {
                    return NotFound(new { error = "Không tìm thấy chiến dịch" });
                }

                if (!await _campaignPostService.CanUserEditCampaign(request.CampaignId, userWallet))
                {
                    return StatusCode(403, new { error = "Bạn không có quyền thực hiện hành động này" });
                }

                // Validate campaign status - should be in Voting status for withdrawal
                if (campaign.Status != InvestDapp.Shared.Enums.CampaignStatus.Voting)
                {
                    return BadRequest(new { error = "Chiến dịch phải ở trạng thái 'Voting' để có thể yêu cầu rút vốn" });
                }

                // Check if there are existing pending withdrawal requests
                var existingPendingRequests = campaign.WithdrawalRequests?.Count(w => w.Status == InvestDapp.Shared.Enums.WithdrawalStatus.Pending) ?? 0;
                if (existingPendingRequests > 0)
                {
                    return BadRequest(new { error = "Đã có yêu cầu rút vốn đang chờ xử lý. Vui lòng chờ hoàn tất trước khi tạo yêu cầu mới." });
                }

                // Check past denial count: if already 2 rejections the campaign is failed and no new request allowed
                var denialCount = campaign.WithdrawalRequests?.Count(w => w.Status == InvestDapp.Shared.Enums.WithdrawalStatus.Rejected) ?? 0;
                if (denialCount >= 2)
                {
                    // Update campaign status to Failed if not already
                    if (campaign.Status != InvestDapp.Shared.Enums.CampaignStatus.Failed)
                    {
                        campaign.Status = InvestDapp.Shared.Enums.CampaignStatus.Failed;
                        await _campaignPostService.UpdateCampaignAsync(campaign);
                    }
                    return BadRequest(new { error = "Chiến dịch đã thất bại do 2 lần yêu cầu rút vốn bị từ chối" });
                }

                // Create withdrawal request record (blockchain transaction already completed)
                request.address = userWallet;
                var withdrawal = await _campain.CreatRerequestWithdrawalAsync(request);

                return Ok(new
                {
                    message = "Yêu cầu rút vốn đã được ghi nhận thành công",
                    campaignId = request.CampaignId,
                    txHash = request.TxHash,
                    requestId = withdrawal?.Id,
                    denialCount = denialCount,
                    warning = denialCount == 1 ? "Lưu ý: Đây là lần thử thứ 2. Nếu bị từ chối lần nữa, chiến dịch sẽ chuyển sang trạng thái Failed." : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi server: " + ex.Message });
            }
        }

        // POST: /Campaigns/UpdateWithdrawalStatus - Update withdrawal request status after blockchain execution
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateWithdrawalStatus([FromBody] UpdateWithdrawalStatusDto request)
        {
            try
            {
                // Require campaign and txHash. RequestId may be omitted or stale from the client.
                if (request == null || request.CampaignId <= 0 || string.IsNullOrEmpty(request.TxHash))
                {
                    return BadRequest(new { error = "Dữ liệu không hợp lệ" });
                }

                var userWallet = User.FindFirst("WalletAddress")?.Value;
                if (string.IsNullOrEmpty(userWallet))
                {
                    return Unauthorized(new { error = "Không tìm thấy địa chỉ ví" });
                }

                // Attempt to ensure RequestId is valid. If client didn't supply a correct RequestId,
                // look up the latest pending withdrawal request for this campaign and use that id.
                var campaign = await _campaignPostService.GetCampaignByIdAsync(request.CampaignId);
                if (campaign == null)
                {
                    return NotFound(new { error = "Không tìm thấy chiến dịch" });
                }

                // If RequestId is not provided or doesn't match any request for this campaign, find the pending one.
                var matching = campaign.WithdrawalRequests?.FirstOrDefault(w => w.Id == request.RequestId && w.CampaignId == request.CampaignId);
                if (matching == null)
                {
                    // Find the most recent pending (status == Pending) request
                    var pending = campaign.WithdrawalRequests?.Where(w => w.Status == InvestDapp.Shared.Enums.WithdrawalStatus.Pending)
                                        .OrderByDescending(w => w.CreatedAt)
                                        .FirstOrDefault();
                    if (pending != null)
                    {
                        request.RequestId = pending.Id;
                    }
                    else
                    {
                        return BadRequest(new { error = "Không tìm thấy yêu cầu rút vốn đang chờ cho chiến dịch này" });
                    }
                }

                var (withdrawalRequest, rejectionCount) = await _campain.UpdateWithdrawalRequestStatusAsync(request);

                return Ok(new
                {
                    message = "Trạng thái withdrawal request đã được cập nhật",
                    campaignId = request.CampaignId,
                    requestId = request.RequestId,
                    newStatus = withdrawalRequest.Status.ToString(),
                    rejectionCount = rejectionCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi server: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Refund([FromBody] RefundRequestDto request)
        {
            try
            {
                if (request == null || request.CampaignId <= 0)
                {
                    return BadRequest(new { error = "Campaign ID không hợp lệ" });
                }

                var campaign = await _campaignPostService.GetCampaignByIdAsync(request.CampaignId);
                if (campaign == null)
                {
                    return NotFound(new { error = "Không tìm thấy chiến dịch" });
                }

                var wallet = User.FindFirst("WalletAddress")?.Value;
                if (string.IsNullOrEmpty(wallet) || campaign.OwnerAddress.ToLower() != wallet.ToLower())
                {
                    return StatusCode(403, new { error = "Không có quyền thực hiện hành động này" });
                }

                campaign.IsRefunded = true;
                campaign.Status = Shared.Enums.CampaignStatus.Failed;

                await _campaignPostService.UpdateCampaignAsync(campaign);

                return Ok(new
                {
                    message = "Yêu cầu hoàn tiền cho nhà đầu tư đã được ghi nhận",
                    campaignId = campaign.Id,
                    success = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi server: " + ex.Message });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClaimRefund(ClaimRefundDto? claimRefund)
        {
            try
            {
                if (claimRefund == null)
                {
                    return BadRequest(new { error = "Dữ liệu không hợp lệ - payload trống" });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.SelectMany(x => x.Value?.Errors.Select(e => e.ErrorMessage) ?? Enumerable.Empty<string>());
                    return BadRequest(new { error = "Dữ liệu không hợp lệ", details = errors });
                }

                if (claimRefund.CampaignId <= 0)
                {
                    return BadRequest(new { error = "Campaign ID không hợp lệ" });
                }

                if (string.IsNullOrEmpty(claimRefund.TransactionHash))
                {
                    return BadRequest(new { error = "Transaction Hash không được để trống" });
                }

                var campaign = await _campaignPostService.GetCampaignByIdAsync(claimRefund.CampaignId);
                if (campaign == null)
                {
                    return NotFound(new { error = "Không tìm thấy chiến dịch" });
                }

                var wallet = User.FindFirst("WalletAddress")?.Value;
                if (string.IsNullOrEmpty(wallet))
                {
                    return StatusCode(403, new { error = "Không có quyền thực hiện hành động này" });
                }
                claimRefund.InvestorAddress = wallet;
                var result = await _campain.ClaimRefundAsync(claimRefund);
                return Ok(new
                {
                    message = "Yêu cầu hoàn tiền đã được ghi nhận",
                    campaignId = claimRefund.CampaignId,
                    txHash = result.TransactionHash,
                    success = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi server: " + ex.Message });
            }
        }

        #region analysis transaction
        public async Task<IActionResult> TaskAnalysis()
        {
            return View();
        }

        // API: Get user's transaction analysis data
        [HttpGet("api/user/transactions")]
        public async Task<IActionResult> GetUserTransactions()
        {
            try
            {
                var wallet = User.FindFirst("WalletAddress")?.Value;
                if (string.IsNullOrEmpty(wallet))
                {
                    return Unauthorized(new { error = "Wallet address not found" });
                }

                var userCampaigns = await _campaignPostService.GetInvestedCampaignsAsync(wallet);

                var transactions = new List<object>();
                double totalInvestedWei = 0;
                double totalRefundedWei = 0;
                int totalRefunds = 0;

                foreach (var campaign in userCampaigns)
                {
                    var userInvestments = campaign.Investments ?? Enumerable.Empty<InvestDapp.Shared.Models.Investment>();

                    foreach (var investment in userInvestments)
                    {
                        totalInvestedWei += investment.Amount;

                        var hasRefund = campaign.Refunds?.Any() ?? false;

                        transactions.Add(new
                        {
                            id = investment.TransactionHash ?? $"inv_{investment.Id}",
                            hash = investment.TransactionHash ?? "",
                            campaignId = campaign.Id,
                            campaignTitle = campaign.Name,
                            method = "INVEST",
                            amountInWei = investment.Amount,
                            amount = investment.Amount,
                            time = investment.Timestamp,
                            status = hasRefund ? "refunded" : "success",
                            isRefunded = hasRefund,
                            createdAt = investment.Timestamp
                        });
                    }

                    var userRefunds = campaign.Refunds ?? Enumerable.Empty<InvestDapp.Shared.Models.Refund>();

                    foreach (var refund in userRefunds)
                    {
                        if (double.TryParse(refund.AmountInWei, out double refundAmount))
                        {
                            totalRefundedWei += refundAmount;
                        }
                        totalRefunds++;

                        transactions.Add(new
                        {
                            id = refund.TransactionHash ?? $"ref_{refund.Id}",
                            hash = refund.TransactionHash ?? "",
                            campaignId = campaign.Id,
                            campaignTitle = campaign.Name,
                            method = "REFUND",
                            amountInWei = refund.AmountInWei,
                            amount = double.TryParse(refund.AmountInWei, out double amt) ? amt : 0,
                            time = refund.ClaimedAt ?? DateTime.UtcNow,
                            status = "success",
                            isRefunded = false,
                            createdAt = refund.ClaimedAt
                        });
                    }

                    var userProfits = campaign.Profits ?? Enumerable.Empty<InvestDapp.Shared.Models.Profit>();

                    foreach (var profit in userProfits)
                    {
                        transactions.Add(new
                        {
                            id = profit.TransactionHash ?? $"profit_{profit.Id}",
                            hash = profit.TransactionHash ?? "",
                            campaignId = campaign.Id,
                            campaignTitle = campaign.Name,
                            method = "REWARD",
                            amountInWei = profit.Amount.ToString(),
                            amount = profit.Amount,
                            time = profit.CreatedAt,
                            status = "success",
                            isRefunded = false,
                            createdAt = profit.CreatedAt
                        });
                    }
                }

                var result = new
                {
                    walletAddress = wallet,
                    totalInvestedWei = totalInvestedWei.ToString("F4"),
                    totalTx = transactions.Count,
                    totalRefundedWei = totalRefundedWei.ToString("F4"),
                    totalRefunds = totalRefunds,
                    transactions = transactions.OrderByDescending(t => ((DateTime?)t.GetType().GetProperty("time")?.GetValue(t)) ?? DateTime.MinValue)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error: " + ex.Message });
            }
        }
        #endregion
    }
}