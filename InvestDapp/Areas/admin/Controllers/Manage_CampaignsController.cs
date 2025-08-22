using InvestDapp.Application.CampaignService;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models.Message;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvestDapp.Areas.admin.Controllers
{
    [Area("Admin")]
    [Route("admin/manage-campaigns")]
    [Authorize(Roles = "Admin")]
    public class Manage_CampaignsController : Controller
    {
        private readonly ICampaignPostService _campaignService;
        private readonly InvestDbContext _db;

        public Manage_CampaignsController(ICampaignPostService campaignService, InvestDbContext db)
        {
            _campaignService = campaignService;
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
    }
}
