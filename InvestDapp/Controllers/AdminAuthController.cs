using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using InvestDapp.Application.AuthService.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InvestDapp.Shared.Security;

namespace InvestDapp.Controllers;

[Route("admin/auth")]
public class AdminAuthController : Controller
{
    private readonly IAdminLoginService _adminLoginService;

    public AdminAuthController(IAdminLoginService adminLoginService)
    {
        _adminLoginService = adminLoginService;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true &&
            User.HasClaim(AuthorizationPolicies.AdminSessionClaim, AuthorizationPolicies.AdminSessionVerified))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        return View();
    }

    [HttpPost("nonce")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateNonce([FromBody] NonceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = "Thiếu địa chỉ ví hợp lệ." });
        }

        var result = await _adminLoginService.GenerateNonceAsync(request.WalletAddress);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Nonce))
        {
            return BadRequest(new { success = false, error = result.Error ?? "Không thể tạo nonce." });
        }

        return Ok(new { success = true, nonce = result.Nonce });
    }

    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify([FromBody] VerifyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = "Thiếu dữ liệu xác thực." });
        }

        var result = await _adminLoginService.SignInWithSignatureAsync(request.WalletAddress, request.Signature);
        if (!result.Success)
        {
            return Unauthorized(new { success = false, error = result.ErrorMessage ?? "Đăng nhập thất bại." });
        }

        return Ok(new { success = true, redirect = result.RedirectUrl ?? "/admin" });
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _adminLoginService.SignOutAsync();
        return RedirectToAction("Login");
    }

    public class NonceRequest
    {
        [Required]
        public string WalletAddress { get; set; } = string.Empty;
    }

    public class VerifyRequest
    {
        [Required]
        public string WalletAddress { get; set; } = string.Empty;

        [Required]
        public string Signature { get; set; } = string.Empty;
    }
}
