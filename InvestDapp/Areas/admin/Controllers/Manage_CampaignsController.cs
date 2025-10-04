using InvestDapp.Application.CampaignService;
using InvestDapp.Application.NotificationService;
using InvestDapp.Application.UserService;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models.Message;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvestDapp.Shared.Security;

namespace InvestDapp.Areas.admin.Controllers
{
    [Area("Admin")]
    [Route("admin/manage-campaigns")]
    [Authorize(Policy = AuthorizationPolicies.RequireModerator)] // Moderator, Fundraiser can view/edit, Admin+ can approve
    public class Manage_CampaignsController : Controller
    {
        private readonly ICampaignPostService _campaignService;
        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;
        private readonly InvestDbContext _db;

        public Manage_CampaignsController(ICampaignPostService campaignService, INotificationService notificationService,IUserService userService, InvestDbContext db)
        {
            _campaignService = campaignService;
            _notificationService = notificationService;
            _userService = userService;
            _db = db;
        }


        [Route("")]
        [Route("index")]
        public async Task<IActionResult> Index(CampaignStatus? status = null, ApprovalStatus? approvalStatus = null, int page = 1)
        {
            try
            {
                const int pageSize = 10;
                var campaigns = await _campaignService.GetCampaignsForAdminAsync(status, approvalStatus, page, pageSize);
                ViewBag.CurrentStatus = status;
                ViewBag.CurrentApprovalStatus = approvalStatus;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                return View(campaigns);
            }
            catch
            {
                TempData["ErrorMessage"] = "Có lỗi khi tải danh sách chiến dịch.";
                return View(new List<InvestDapp.Models.Campaign>());
            }
        }

        [Route("approve/{id}")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.RequireAdmin)] // Only Admin/SuperAdmin can approve
        public async Task<IActionResult> ApproveCampaign(int id, string? adminNotes = null)
        {

            try
            {
                var adminWallet = User.FindFirst("WalletAddress")?.Value ?? "admin@system.com";
                var success = await _campaignService.ApproveCampaignAsync(id, adminWallet, adminNotes);
                if (!success)
                {
                    TempData["ErrorMessage"] = "Không thể phê duyệt chiến dịch.";
                    return RedirectToAction("Index");
                }

                // Prefer to notify the campaign owner. Fall back to current admin user if owner not found.
                var camp = await _campaignService.GetCampaignByIdAsync(id);
                var ownerResponse = camp != null
                    ? await _userService.GetUserByWalletAddressAsync(camp.OwnerAddress)
                    : null;

                if (ownerResponse?.Data != null)
                {
                    var noti = new CreateNotificationRequest
                    {
                        UserId = ownerResponse.Data.ID,
                        Type = "CampaignApproved",
                        Title = "Chiến dịch của bạn đã được phê duyệt",
                        Message = $"Chiến dịch của bạn đã được phê duyệt bởi quản trị viên.",
                        Data = $"{{\"campaignId\":{id}}}"
                    };

                    var resp = await _notificationService.CreateNotificationAsync(noti);
                    if (resp == null)
                    {
                        TempData["ErrorMessage"] = "Không thể tạo thông báo (null response).";
                    }
                    else if (!resp.Success)
                    {
                        TempData["ErrorMessage"] = "Không thể tạo thông báo: " + resp.Message;
                    }
                }


                // Tạo group chat cho campaign nếu chưa có
                var campaign = await _campaignService.GetCampaignByIdAsync(id);
                if (campaign != null)
                {
                    var existing = await _db.Conversations.FirstOrDefaultAsync(c => c.CampaignId == campaign.Id);
                    if (existing == null)
                    {
                        var owner = await _db.Users.FirstOrDefaultAsync(u => u.WalletAddress == campaign.OwnerAddress);
                        if (owner != null)
                        {
                            var convo = new Conversation
                            {
                                Type = ConversationType.Group,
                                Name = campaign.Name,
                                CampaignId = campaign.Id
                            };
                            _db.Conversations.Add(convo);
                            await _db.SaveChangesAsync();

                            _db.Participants.Add(new Participant
                            {
                                ConversationId = convo.ConversationId,
                                UserId = owner.ID,
                                Role = ParticipantRole.Admin,
                                JoinedAt = DateTime.UtcNow
                            });
                            await _db.SaveChangesAsync();
                        }
                    }
                }

                TempData["SuccessMessage"] = "Chiến dịch đã được phê duyệt thành công!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi khi phê duyệt chiến dịch: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [Route("reject/{id}")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.RequireAdmin)] // Only Admin/SuperAdmin can reject
        public async Task<IActionResult> RejectCampaign(int id, string adminNotes)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(adminNotes))
                {
                    TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối.";
                    return RedirectToAction("Index");
                }
                var adminWallet = User.FindFirst("WalletAddress")?.Value ?? "admin@system.com";
                await _campaignService.RejectCampaignAsync(id, adminWallet, adminNotes);
                TempData["SuccessMessage"] = "Chiến dịch đã bị từ chối.";
                // Prefer to notify the campaign owner. Fall back to current admin user if owner not found.
                var campR = await _campaignService.GetCampaignByIdAsync(id);
                var ownerResponse = campR != null
                    ? await _userService.GetUserByWalletAddressAsync(campR.OwnerAddress)
                    : null;

                if (ownerResponse?.Data != null)
                {
                    var noti = new CreateNotificationRequest
                    {
                        UserId = ownerResponse.Data.ID,
                        Type = "CampaignRejected",
                        Title = "Chiến dịch của bạn đã bị từ chối",
                        Message = $"Chiến dịch của bạn đã bị từ chối bởi quản trị viên. Lý do: {adminNotes}",
                        Data = $"{{\"campaignId\":{id}}}"
                    };
                    await _notificationService.CreateNotificationAsync(noti);
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi khi từ chối chiến dịch: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [Route("details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var campaign = await _campaignService.GetCampaignByIdAsync(id);
                if (campaign == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy chiến dịch.";
                    return RedirectToAction("Index");
                }
                return View(campaign);
            }
            catch
            {
                TempData["ErrorMessage"] = "Có lỗi khi tải thông tin chiến dịch.";
                return RedirectToAction("Index");
            }
        }

        [Route("notify-investors/{id}")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifyInvestors(int id, string? title = null, string? message = null)
        {
            try
            {
                var campaign = await _campaignService.GetCampaignByIdAsync(id);
                if (campaign == null)
                {
                    TempData["ErrorMessage"] = "Chiến dịch không tồn tại.";
                    return RedirectToAction("Index");
                }

                var notiReq = new CreateNotificationToCampaignRequest
                {
                    CampaignId = id,
                    Title = title ?? $"Cập nhật cho chiến dịch {campaign.Name}",
                    Message = message ?? "Có cập nhật mới liên quan tới chiến dịch bạn đã đầu tư.",
                    Type = "CampaignBroadcast"
                };

                var resp = await _notificationService.CreateNotificationForCampaignInvestorsAsync(notiReq);
                if (resp == null || !resp.Success)
                {
                    TempData["ErrorMessage"] = "Không thể gửi thông báo tới nhà đầu tư: " + (resp?.Message ?? "Unknown");
                }
                else
                {
                    if (resp.Data != null && resp.Data.GetType().GetProperty("Sent")?.GetValue(resp.Data) is int sent)
                    {
                        TempData["SuccessMessage"] = $"Thông báo đã gửi tới {sent} nhà đầu tư.";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "Thông báo đã gửi tới nhà đầu tư.";
                    }
                }

                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi gửi thông báo: " + ex.Message;
                return RedirectToAction("Details", new { id });
            }
        }
    }
}
