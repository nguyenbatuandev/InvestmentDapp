using InvestDapp.Application.KycService;
using InvestDapp.Shared.Common.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestDapp.Controllers
{
    [Authorize]
    public class KycController : Controller
    {
        private readonly IKycService kycService;
        public KycController(IKycService  kycRepository)
        {
            kycService = kycRepository;
        }
        public IActionResult FundraiserKycView()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitKyc([FromForm] FundraiserKycRequest kycRequest)
        {

            // Lấy địa chỉ ví từ Claims
            var wallet = User.FindFirst("WalletAddress")?.Value;
            if (string.IsNullOrEmpty(wallet))
            {
                return Unauthorized(new { message = "Không thể xác thực địa chỉ ví của người dùng." });
            }
            if (!ModelState.IsValid)
            {
                // Trả về một object lỗi có cấu trúc để JS dễ xử lý
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return BadRequest(new { message = "Dữ liệu không hợp lệ.", errors });
            }

            var kyc = await kycService.SubmitKycAsync(kycRequest, wallet);
            if (!kyc.Success)
            {
                return BadRequest(kyc);
            }
            return Ok(kyc);
        }

        [HttpGet]
        public async Task<IActionResult> CheckKyc()
        {
            // Lấy địa chỉ ví từ Claims
            var wallet = User.FindFirst("WalletAddress")?.Value;
            if (string.IsNullOrEmpty(wallet))
            {
                return Unauthorized(new { message = "Không thể xác thực địa chỉ ví của người dùng." });
            }
            var kyc = await kycService.CheckKycAsync(wallet);
            return Ok(kyc);
        }
    }

}
