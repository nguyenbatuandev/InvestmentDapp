using InvestDapp.Application.TradingServices.Admin;
using InvestDapp.Shared.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InvestDapp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "RequireAdmin")]
    [Route("admin/trading")]
    public class TradingManagementController : Controller
    {
        private readonly IAdminTradingService _tradingService;
        private readonly ILogger<TradingManagementController> _logger;

        public TradingManagementController(
            IAdminTradingService tradingService,
            ILogger<TradingManagementController> logger)
        {
            _tradingService = tradingService;
            _logger = logger;
        }

        private string GetAdminWallet()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "UNKNOWN";
        }

        // GET: /admin/trading
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var dashboard = await _tradingService.GetDashboardAsync();
                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading trading dashboard");
                TempData["Error"] = "Không thể tải dashboard";
                return View();
            }
        }

        // GET: /admin/trading/users
        [HttpGet("users")]
        public async Task<IActionResult> Users(int page = 1)
        {
            try
            {
                var traders = await _tradingService.GetAllTradersAsync(page, 50);
                ViewBag.CurrentPage = page;
                return View(traders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading traders list");
                return View(new List<TopTraderDto>());
            }
        }

        // GET: /admin/trading/user/{wallet}
        [HttpGet("user/{wallet}")]
        public async Task<IActionResult> UserDetail(string wallet)
        {
            try
            {
                var detail = await _tradingService.GetUserDetailAsync(wallet);
                if (detail == null)
                {
                    TempData["Error"] = "Không tìm thấy user";
                    return RedirectToAction(nameof(Users));
                }
                return View(detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user detail for {Wallet}", wallet);
                TempData["Error"] = "Không thể tải thông tin user";
                return RedirectToAction(nameof(Users));
            }
        }

        // GET: /admin/trading/withdrawals
        [HttpGet("withdrawals")]
        public async Task<IActionResult> Withdrawals()
        {
            try
            {
                var pending = await _tradingService.GetPendingWithdrawalsAsync();
                return View(pending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading withdrawals");
                return View(new List<PendingWithdrawalDto>());
            }
        }

        // POST: /admin/trading/withdrawals/approve
        [HttpPost("withdrawals/approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveWithdrawal([FromForm] ApproveWithdrawalRequest request)
        {
            try
            {
                var success = await _tradingService.ApproveWithdrawalAsync(request, GetAdminWallet());
                if (success)
                {
                    TempData["Success"] = "Đã duyệt yêu cầu rút tiền";
                }
                else
                {
                    TempData["Error"] = "Không thể duyệt yêu cầu";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving withdrawal");
                TempData["Error"] = "Có lỗi xảy ra";
            }
            return RedirectToAction(nameof(Withdrawals));
        }

        // POST: /admin/trading/withdrawals/reject
        [HttpPost("withdrawals/reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectWithdrawal([FromForm] RejectWithdrawalRequest request)
        {
            try
            {
                var success = await _tradingService.RejectWithdrawalAsync(request, GetAdminWallet());
                if (success)
                {
                    TempData["Success"] = "Đã từ chối yêu cầu rút tiền";
                }
                else
                {
                    TempData["Error"] = "Không thể từ chối yêu cầu";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting withdrawal");
                TempData["Error"] = "Có lỗi xảy ra";
            }
            return RedirectToAction(nameof(Withdrawals));
        }

        // GET: /admin/trading/locks
        [HttpGet("locks")]
        public async Task<IActionResult> Locks()
        {
            try
            {
                var locks = await _tradingService.GetActiveLocksAsync();
                return View(locks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading locks");
                return View();
            }
        }

        // POST: /admin/trading/lock-account
        [HttpPost("lock-account")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LockAccount([FromForm] LockAccountRequest request)
        {
            try
            {
                var success = await _tradingService.LockAccountAsync(request, GetAdminWallet());
                if (success)
                {
                    TempData["Success"] = $"Đã khóa tài khoản {request.UserWallet}";
                }
                else
                {
                    TempData["Error"] = "Không thể khóa tài khoản";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking account");
                TempData["Error"] = "Có lỗi xảy ra";
            }
            return RedirectToAction(nameof(UserDetail), new { wallet = request.UserWallet });
        }

        // POST: /admin/trading/unlock-account
        [HttpPost("unlock-account")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockAccount([FromForm] UnlockAccountRequest request)
        {
            try
            {
                var success = await _tradingService.UnlockAccountAsync(request, GetAdminWallet());
                if (success)
                {
                    TempData["Success"] = "Đã mở khóa tài khoản";
                }
                else
                {
                    TempData["Error"] = "Không thể mở khóa tài khoản";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking account");
                TempData["Error"] = "Có lỗi xảy ra";
            }
            return RedirectToAction(nameof(Locks));
        }

        // POST: /admin/trading/adjust-balance
        [HttpPost("adjust-balance")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustBalance([FromForm] AdjustBalanceRequest request)
        {
            try
            {
                var success = await _tradingService.AdjustUserBalanceAsync(request, GetAdminWallet());
                if (success)
                {
                    TempData["Success"] = "Đã điều chỉnh số dư";
                }
                else
                {
                    TempData["Error"] = "Không thể điều chỉnh số dư";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adjusting balance");
                TempData["Error"] = "Có lỗi xảy ra";
            }
            return RedirectToAction(nameof(UserDetail), new { wallet = request.UserWallet });
        }

        // GET: /admin/trading/fee-config
        [HttpGet("fee-config")]
        public async Task<IActionResult> FeeConfig()
        {
            try
            {
                var config = await _tradingService.GetActiveFeeConfigAsync();
                return View(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fee config");
                return View();
            }
        }

        // POST: /admin/trading/fee-config
        [HttpPost("fee-config")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFeeConfig([FromForm] UpdateFeeConfigRequest request)
        {
            try
            {
                // Log received values for debugging
                _logger.LogInformation("Received fee config update - Maker: {Maker}, Taker: {Taker}, Withdrawal: {Withdrawal}, MinFee: {MinFee}, MinAmount: {MinAmount}, MaxAmount: {MaxAmount}, DailyLimit: {DailyLimit}",
                    request.MakerFeePercent, request.TakerFeePercent, request.WithdrawalFeePercent,
                    request.MinWithdrawalFee, request.MinWithdrawalAmount, request.MaxWithdrawalAmount, request.DailyWithdrawalLimit);

                // Validate decimal values
                if (request.MakerFeePercent <= 0 || request.TakerFeePercent <= 0 || request.WithdrawalFeePercent <= 0)
                {
                    TempData["Error"] = "Các giá trị phí phải lớn hơn 0. Vui lòng nhập số thập phân đúng định dạng (ví dụ: 0.02)";
                    return RedirectToAction(nameof(FeeConfig));
                }

                var success = await _tradingService.UpdateFeeConfigAsync(request, GetAdminWallet());
                if (success)
                {
                    TempData["Success"] = "Đã cập nhật cấu hình phí";
                }
                else
                {
                    TempData["Error"] = "Không thể cập nhật cấu hình";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating fee config");
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
            }
            return RedirectToAction(nameof(FeeConfig));
        }

        // API Endpoints for AJAX
        [HttpGet("api/dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var dashboard = await _tradingService.GetDashboardAsync();
                return Json(new { success = true, data = dashboard.Stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("api/pending-withdrawals")]
        public async Task<IActionResult> GetPendingWithdrawals()
        {
            try
            {
                var pending = await _tradingService.GetPendingWithdrawalsAsync();
                return Json(new { success = true, data = pending });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending withdrawals");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("api/user/{wallet}")]
        public async Task<IActionResult> GetUserDetail(string wallet)
        {
            try
            {
                var detail = await _tradingService.GetUserDetailAsync(wallet);
                if (detail == null)
                {
                    return Json(new { success = false, error = "User not found" });
                }
                return Json(new { success = true, data = detail });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user detail");
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
