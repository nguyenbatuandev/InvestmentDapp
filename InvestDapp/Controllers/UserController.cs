using InvestDapp.Application.NotificationService;
using InvestDapp.Application.UserService;
using InvestDapp.Shared.Common.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestDapp.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        public UserController(ILogger<UserController> logger, IUserService userService, INotificationService notificationService)
        {
            _logger = logger;
            _userService = userService;
            _notificationService = notificationService;
        }
        public IActionResult Profile()
        {
            return View();
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId");
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile([FromBody] UserUpdateRequest request)
        {
            var wallet = User.FindFirst("WalletAddress")?.Value;
            var user = await _userService.UpdateUserAsync(request, wallet);

            try
            {
                int userId = await _userService.GetCurrentUserId();
                _logger.LogInformation("UpdateProfile called for UserId={UserId}", userId);
            
                var notification = new CreateNotificationRequest
                {
                    UserId = userId,
                    Title = "Cập nhật hồ sơ",
                    Message = "Hồ sơ của bạn đã được cập nhật thành công.",
                    Type = "ProfileUpdate"
                };

                var notifyResult = await _notificationService.CreateNotificationAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create notification for profile update");
            }

            if (!user.Success)
            {
                _logger.LogError("Lỗi khi cập nhật hồ sơ cho địa chỉ ví: {WalletAddress}. Lỗi: {ErrorMessage}", wallet, user.Message);
                return BadRequest(new { success = false, message = user.Message });
            }

            return Ok(new { 
                success = true, 
                message = "Hồ sơ đã được cập nhật thành công",
                data = new {
                    id = user.Data?.ID,
                    name = user.Data?.Name,
                    email = user.Data?.Email,
                    avatar = user.Data?.Avatar,
                    bio = user.Data?.Bio,
                    walletAddress = user.Data?.WalletAddress
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetUserProfile()
        {
            var user = await _userService.GetCurrentUserId();
            var result = await _userService.GetUserByIdAsync(user);

            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(new {
                id = result.Data?.ID,
                name = result.Data?.Name,
                email = result.Data?.Email,
                avatar = result.Data?.Avatar,
                bio = result.Data?.Bio,
                walletAddress = result.Data?.WalletAddress,
                role = result.Data?.Role,
                createdAt = result.Data?.CreatedAt,
                updatedAt = result.Data?.UpdatedAt
            }); 
        }

        // GET: /User/Notifications
        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var userId = await _userService.GetCurrentUserId();

            var result = await _userService.GetNotificationsAsync(userId);
            if (!result.Success) return BadRequest(result.Message);

            // Return safe notification data without circular references
            var safeNotifications = result.Data?.Select(n => new
            {
                id = n.ID,
                title = n.Title,
                message = n.Message,
                type = n.Type,
                data = n.Data,
                isRead = n.IsRead,
                createdAt = n.CreatedAt
            }).ToList();

            return Ok(safeNotifications);
        }

        // POST: /User/MarkNotificationRead
        [HttpPost]
        public async Task<IActionResult> MarkNotificationRead([FromBody] MarkNotificationRequest req)
        {
            var userId = await _userService.GetCurrentUserId();

            if (req == null || req.NotificationId == 0) return BadRequest("Invalid request");

            var result = await _userService.MarkNotificationAsReadAsync(userId, req.NotificationId);
            if (!result.Success) return BadRequest(result.Message);

            // Send unread count update via SignalR after marking as read
            try
            {
                var notificationsResponse = await _userService.GetNotificationsAsync(userId);
                if (notificationsResponse.Success && notificationsResponse.Data != null)
                {
                    var unreadCount = notificationsResponse.Data.Count(n => !n.IsRead);
                    await _notificationService.SendUnreadCountUpdateAsync(userId.ToString(), unreadCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send unread count update after marking notification as read");
            }

            return Ok(new { success = true });
        }

        // POST: /User/ClearReadNotifications
        [HttpPost]
        public async Task<IActionResult> ClearReadNotifications()
        {
            var userId = await _userService.GetCurrentUserId();

            try
            {
                var result = await _userService.DeleteReadNotificationsAsync(userId);
                if (!result.Success) return BadRequest(result.Message);

                try
                {
                    var notificationsResponse = await _userService.GetNotificationsAsync(userId);
                    if (notificationsResponse.Success && notificationsResponse.Data != null)
                    {
                        var unreadCount = notificationsResponse.Data.Count(n => !n.IsRead);
                        await _notificationService.SendUnreadCountUpdateAsync(userId.ToString(), unreadCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send unread count update after deleting read notifications");
                }

                return Ok(new { success = true, message = "Đã xóa thông báo đã đọc" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear read notifications for user {UserId}", userId);
                return BadRequest("Có lỗi khi xóa thông báo");
            }
        }

        // GET: /User/GetUnreadNotificationCount
        [HttpGet]
        public async Task<IActionResult> GetUnreadNotificationCount()
        {
            try
            {
                var userId = await _userService.GetCurrentUserId();
                var notificationsResponse = await _userService.GetNotificationsAsync(userId);
                if (!notificationsResponse.Success || notificationsResponse.Data == null)
                {
                    return Json(new { count = 0 });
                }
                
                var unreadCount = notificationsResponse.Data.Count(n => !n.IsRead);
                return Json(new { count = unreadCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notification count");
                return Json(new { count = 0 });
            }
        }

    }
}
