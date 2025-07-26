using InvestDapp.Application.AuthService;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            if (string.IsNullOrEmpty(wallet))
            {
                return Json(new { exists = false, message = "Wallet address cannot be empty." });
            }

            var profile = await _userRepository.GetUserByWalletAddressAsync(wallet);
            if (profile != null)
            {
                await _authService.SignInUser(profile);
                return Json(new { exists = true });
            }

            return Json(new { exists = false });
        }

        [HttpPost]
        public async Task<JsonResult> SaveProfile(string wallet, string name, string email)
        {
            try
            {
                if (string.IsNullOrEmpty(wallet) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email))
                {
                    return Json(new { success = false, message = "Invalid data provided." });
                }

                var user = await _userRepository.GetUserByWalletAddressAsync(wallet);

                if (user != null)
                {
                    return Json(new { success = true, message = "This wallet address is already registered." });
                }

                var profile = await _userRepository.CreateUserAsync(wallet, name, email);

                if (profile != null)
                {
                    await _authService.SignInUser(profile);
                    return Json(new { success = true, message = "Profile saved successfully." });
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