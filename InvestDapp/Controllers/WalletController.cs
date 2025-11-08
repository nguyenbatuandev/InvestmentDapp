using InvestDapp.Application.AuthService;
using InvestDapp.Infrastructure.Data.interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;


namespace InvestDapp.Controllers
{
    public class WalletController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IUser _userRepository;
        public WalletController(IAuthService authService, IUser userRepository)
        {
            _authService = authService;
            _userRepository = userRepository;
        }

        // ==========================================
        // NEW: Wallet Authentication with Signature
        // ==========================================

        /// <summary>
        /// Generate nonce for wallet authentication
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Nonce([FromBody] NonceRequest request)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.WalletAddress))
            {
                return BadRequest(new { success = false, error = "Địa chỉ ví không hợp lệ." });
            }

            var result = await _authService.GenerateUserNonceAsync(request.WalletAddress);
            
            if (!result.Success)
            {
                return BadRequest(new { success = false, error = result.Error ?? "Không thể tạo nonce." });
            }

            return Ok(new { success = true, nonce = result.Nonce });
        }

        /// <summary>
        /// Verify signature and sign in user
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Verify([FromBody] VerifyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, error = "Thiếu dữ liệu xác thực." });
            }

            var result = await _authService.VerifyUserSignatureAsync(request.WalletAddress, request.Signature);
            
            if (!result.Success)
            {
                return Unauthorized(new { success = false, error = result.ErrorMessage ?? "Xác thực thất bại." });
            }

            // Get user profile to check if needs profile completion
            var user = await _userRepository.GetUserByWalletAddressAsync(request.WalletAddress);
            var requiresProfile = user == null || string.IsNullOrWhiteSpace(user.Name) || string.IsNullOrWhiteSpace(user.Email);

            return Ok(new 
            { 
                success = true, 
                requiresProfile,
                profile = user != null ? new
                {
                    name = user.Name,
                    email = user.Email,
                    wallet = user.WalletAddress
                } : null
            });
        }

        // ==========================================
        // LEGACY: Keep for backward compatibility but mark as deprecated
        // ==========================================


        [HttpGet]
        public async Task<JsonResult> CheckWallet(string wallet)
        {
            if (string.IsNullOrWhiteSpace(wallet))
            {
                return Json(new { exists = false, error = "Wallet address cannot be empty." });
            }

            var existingProfile = await _userRepository.GetUserByWalletAddressAsync(wallet);
            var isNew = existingProfile == null;

            var profile = existingProfile ?? await _userRepository.EnsureUserAsync(wallet);

            if (profile == null)
            {
                return Json(new { exists = false, error = "Unable to initialize wallet profile." });
            }

            await _authService.SignInUser(profile);

            var requiresProfile = string.IsNullOrWhiteSpace(profile.Name) || string.IsNullOrWhiteSpace(profile.Email);

            return Json(new
            {
                exists = true,
                isNew,
                requiresProfile,
                profile = new
                {
                    name = profile.Name,
                    email = profile.Email,
                    wallet = profile.WalletAddress
                }
            });
        }

        [HttpPost]
        public async Task<JsonResult> SaveProfile(string wallet, string name, string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(wallet))
                {
                    return Json(new { success = false, message = "Wallet address is required." });
                }

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
                {
                    return Json(new { success = false, message = "Please provide both name and email." });
                }

                var profile = await _userRepository.UpdateUserProfileAsync(wallet, name, email);

                if (profile != null)
                {
                    await _authService.SignInUser(profile);
                    return Json(new
                    {
                        success = true,
                        message = "Profile saved successfully.",
                        profile = new
                        {
                            name = profile.Name,
                            email = profile.Email,
                            wallet = profile.WalletAddress
                        }
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to create profile." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }

        // ==========================================
        // Request Models
        // ==========================================

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

}