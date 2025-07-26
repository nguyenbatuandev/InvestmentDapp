using InvestDapp.Application.UserService;
using InvestDapp.Shared.Common.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace InvestDapp.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly IUserService _userService;
        public UserController(ILogger<UserController> logger, IUserService userService)
        {
            _logger = logger;
            _userService = userService;
        }
        public IActionResult Profile()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile([FromBody] UserUpdateRequest request)
        {
            var wallet = User.FindFirst("WalletAddress")?.Value;
            var user = await _userService.UpdateUserAsync(request, wallet);
            if (!user.Success)
            {
                _logger.LogError("Không tìm thấy người dùng với địa chỉ ví: {WalletAddress}", wallet);
                return BadRequest(user);
            }
            return Ok(user);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserProfile()
        {
            var wallet = User.FindFirst("WalletAddress")?.Value;

            if (string.IsNullOrEmpty(wallet))
            {
                return Unauthorized("WalletAddress claim not found.");
            }

            var result = await _userService.GetUserByWalletAddressAsync(wallet);

            if (!result.Success)
            {
                return BadRequest(result.Message);
            }

            return Ok(result.Data); // assuming your result has a .Data property
        }

    }
}
