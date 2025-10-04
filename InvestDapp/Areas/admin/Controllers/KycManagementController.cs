using InvestDapp.Application.KycService;
using InvestDapp.Shared.Common.Request;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InvestDapp.Shared.Security;

namespace InvestDapp.Areas.admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AuthorizationPolicies.RequireModerator)] // Moderator can view, Admin+ can approve
    [Route("admin/kyc-management")]
    public class KycManagementController : Controller
    {
        private readonly IKycService _kycService;
        private readonly IAntiforgery _antiforgery;

        public KycManagementController(IKycService kycService, IAntiforgery antiforgery)
        {
            _kycService = kycService;
            _antiforgery = antiforgery;
        }

        [HttpGet("")]
        [HttpGet("index")]
        public IActionResult Index()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            ViewData["RequestVerificationToken"] = tokens.RequestToken;
            return View();
        }

        [HttpGet("list")]
        public async Task<IActionResult> List([FromQuery] KycAdminFilterRequest filter)
        {
            var result = await _kycService.QueryKycsAsync(filter);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("{id:int}/approve")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.RequireAdmin)] // Only Admin/SuperAdmin can approve
        public async Task<IActionResult> Approve(int id)
        {
            var result = await _kycService.ApproveKycAsync(id);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("{id:int}/reject")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.RequireAdmin)] // Only Admin/SuperAdmin can reject
        public async Task<IActionResult> Reject(int id)
        {
            var result = await _kycService.RejectKycAsync(id);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("{id:int}/revoke")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(int id)
        {
            var result = await _kycService.RevokeKycAsync(id);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
