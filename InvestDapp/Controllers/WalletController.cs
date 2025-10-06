using InvestDapp.Application.AuthService;
using InvestDapp.Infrastructure.Data.interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;


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
    }

}