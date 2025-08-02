using InvestDapp.Application.CampaignService;
using InvestDapp.Application.UserService;
using InvestDapp.Shared.Common.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestDapp.Controllers
{
    [Authorize]
    public class CampaignsController : Controller
    {
        private readonly ICampaignPostService _campaignPostService;
        private readonly IUserService _userService;

        public CampaignsController(
            ICampaignPostService campaignPostService,
            IUserService userService)
        {
            _campaignPostService = campaignPostService;
            _userService = userService;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }


        // GET: /Campaign/Create
        public IActionResult Create()
        {
            return View();
        }

        // GET: /Campaign/MyCampaigns
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


        #region Campaign API

        // POST: /Campaign/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
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

                // Redirect to CreatePost instead of MyCampaigns
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

                // Kiểm tra xem đây có phải bài viết đầu tiên không
                var campaign = await _campaignPostService.GetCampaignByIdAsync(request.CampaignId);
                var allPosts = await _campaignPostService.GetPostsByCampaignIdAsync(request.CampaignId);
                bool isFirstPost = allPosts.Count() == 1; // Chỉ có 1 bài viết là bài vừa tạo

                if (isFirstPost)
                {
                    TempData["SuccessMessage"] = "Bài viết đầu tiên đã được tạo và tự động phê duyệt! Chiến dịch hiện đang chờ admin duyệt.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Bài viết đã được tạo thành công và đang chờ admin duyệt.";
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
        public async Task<IActionResult> DeletePost(int id)
        {
            try
            {
                var wallet = User.FindFirst("WalletAddress")?.Value;
                var post = await _campaignPostService.GetPostByIdAsync(id);

                if (post == null)
                {
                    return NotFound();
                }

                await _campaignPostService.DeletePostAsync(id, wallet);
                TempData["SuccessMessage"] = "Bài viết đã được xóa thành công.";

                return RedirectToAction("Posts", new { campaignId = post.CampaignId });
            }
            catch (UnauthorizedAccessException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi xóa bài viết.";
                return RedirectToAction("Index", "Home");
            }
        }

        #endregion

        #region Workflow Views

        // GET: /Campaign/AwaitingApproval/5
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
    }
}