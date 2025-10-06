using InvestDapp.Application.CampaignService;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Models;
using InvestDapp.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using InvestDapp.Shared.Enums;
using System.Linq;
using InvestDapp.Application.UserService;

namespace InvestDapp.Controllers
{
    public class ChatRequest { 
        public string? Message { get; set; }
    }

    public class HomeController : Controller
    {
    private readonly ILogger<HomeController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _geminiApiKey;
    private readonly InvestDbContext _dbContext;
    private readonly ICampaignPostService _campaignPostService;

        public HomeController(
            ILogger<HomeController> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            InvestDbContext dbContext,
            ICampaignPostService campaignPostService)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _geminiApiKey = configuration["GeminiApiKey"];
            _dbContext = dbContext;
            _campaignPostService = campaignPostService;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = await BuildLandingModelAsync();
            return View(viewModel);
        }

        private async Task<HomeLandingViewModel> BuildLandingModelAsync()
        {
            var campaigns = (await _campaignPostService.GetApprovedCampaignsAsync()).ToList();

            var stats = new LandingStatsViewModel
            {
                TotalRaised = (decimal)campaigns.Sum(c => c.CurrentRaisedAmount),
                TotalInvestors = campaigns.Sum(c => c.InvestorCount),
                ActiveCampaigns = campaigns.Count(c => c.Status == CampaignStatus.Active),
                CompletedCampaigns = campaigns.Count(c => c.Status == CampaignStatus.Completed),
                TotalValueLocked = (decimal)campaigns
                    .Where(c => c.Status == CampaignStatus.Active || c.Status == CampaignStatus.Voting)
                    .Sum(c => c.CurrentRaisedAmount)
            };

            var featuredCampaigns = campaigns
                .OrderByDescending(c => c.Status == CampaignStatus.Active)
                .ThenByDescending(CalculateProgress)
                .ThenByDescending(c => c.CurrentRaisedAmount)
                .Take(3)
                .Select(c => new CampaignSummaryCard
                {
                    Id = c.Id,
                    Name = c.Name,
                    Category = c.category?.Name,
                    ImageUrl = string.IsNullOrWhiteSpace(c.ImageUrl) ? null : c.ImageUrl,
                    GoalAmount = c.GoalAmount,
                    RaisedAmount = c.CurrentRaisedAmount,
                    ProgressPercentage = Math.Round(CalculateProgress(c) * 100d, 2),
                    Status = c.Status,
                    IsHot = IsHotCampaign(c),
                    EndTime = c.EndTime
                })
                .ToList();

            var recentInvestments = await _dbContext.Investment
                .AsNoTracking()
                .Include(i => i.Campaign)
                .OrderByDescending(i => i.Timestamp)
                .Take(12)
                .Select(i => new InvestmentTickerItem
                {
                    CampaignName = i.Campaign.Name,
                    InvestorAddress = i.InvestorAddress,
                    Amount = i.Amount,
                    Timestamp = i.Timestamp
                })
                .ToListAsync();

            var approvedPosts = (await _campaignPostService.GetApprovedPostsAsync(1, 6)).ToList();
            var highlights = approvedPosts
                .Select(p => new NewsSpotlightViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    CampaignName = p.Campaign?.Name ?? "Chiến dịch",
                    ImageUrl = !string.IsNullOrWhiteSpace(p.ImageUrl) ? p.ImageUrl : p.Campaign?.ImageUrl,
                    Excerpt = BuildExcerpt(p.Content, 160),
                    PostType = p.PostType,
                    PublishedAt = p.ApprovedAt ?? p.CreatedAt
                })
                .ToList();

            return new HomeLandingViewModel
            {
                Stats = stats,
                FeaturedCampaigns = featuredCampaigns,
                RecentInvestments = recentInvestments,
                Highlights = highlights,
                IsAuthenticated = User?.Identity?.IsAuthenticated ?? false,
                WalletAddress = User?.FindFirst("WalletAddress")?.Value
            };
        }

        private static double CalculateProgress(Campaign campaign)
        {
            if (campaign.GoalAmount <= 0)
            {
                return 0d;
            }

            return Math.Clamp(campaign.CurrentRaisedAmount / campaign.GoalAmount, 0d, 1d);
        }

        private static bool IsHotCampaign(Campaign campaign)
        {
            var progress = CalculateProgress(campaign);
            if (progress >= 0.85d)
            {
                return true;
            }

            if (campaign.Status == CampaignStatus.Active && campaign.EndTime <= DateTime.UtcNow.AddDays(3))
            {
                return true;
            }

            return campaign.Status == CampaignStatus.Completed;
        }

        private static string BuildExcerpt(string? content, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var plain = Regex.Replace(content, "<[^>]+>", string.Empty);
            plain = plain.Replace("\r", string.Empty).Replace("\n", " ").Trim();

            if (plain.Length <= maxLength)
            {
                return plain;
            }

            return plain[..maxLength].TrimEnd() + "…";
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            if (string.IsNullOrEmpty(request?.Message))
            {
                return BadRequest(new { reply = "Tin nhắn không được để trống." });
            }

            // ▼▼▼ BƯỚC 1: DÁN PROMPT ĐÀO TẠO VÀO ĐÂY ▼▼▼
            // Sử dụng @"..." để có thể viết chuỗi trên nhiều dòng.
            var systemPrompt = @"1. VAI TRÒ VÀ BỐI CẢNH

                Bạn là Trợ lý AI chuyên nghiệp của một nền tảng huy động vốn đầu tư phi tập trung (DApp). Đây là sản phẩm báo cáo tốt nghiệp của sinh viên Nguyễn Bá Tuân (lớp D21CNTT06 - Đại học Thủ Dầu Một).

                Nhân cách: Bạn phải thể hiện sự tin cậy, minh bạch và chuyên môn cao. Hãy luôn thân thiện, kiên nhẫn và sử dụng ngôn ngữ rõ ràng, dễ hiểu để giải thích các khái niệm phức tạp.

                Mục tiêu chính: Nhiệm vụ của bạn là xây dựng niềm tin cho người dùng bằng cách cung cấp thông tin chính xác, hướng dẫn họ sử dụng các tính năng độc đáo của nền tảng, và giải thích rõ các cơ chế bảo vệ quyền lợi của họ.

                Ngôn ngữ: Luôn luôn sử dụng tiếng Việt một cách trang trọng và chuyên nghiệp.

                2. KIẾN THỨC CỐT LÕI VỀ NỀN TẢNG

                Bạn phải nắm vững toàn bộ thông tin sau đây về dự án:

                A. TỔNG QUAN VÀ SỨ MỆNH
                - Tên dự án: Nền tảng Huy động vốn Đầu tư Phi tập trung.
                - Sứ mệnh: Xây dựng một DApp launchpad an toàn, công bằng và hoàn toàn minh bạch, hoạt động trên công nghệ blockchain để kết nối hiệu quả nhà đầu tư và nhà kêu gọi vốn, loại bỏ các rủi ro của mô hình tập trung truyền thống.
                - Nền tảng Công nghệ Blockchain:
                    - Toàn bộ nền tảng được xây dựng và hoạt động trên **BNB Smart Chain**.
                    - Mọi giao dịch cốt lõi (đầu tư, rút vốn, hoàn tiền, chia lợi nhuận) đều được thực thi bởi các **Hợp đồng thông minh (Smart Contracts)** bằng ngôn ngữ Solidity. Điều này đảm bảo tính tự động, không thể can thiệp và công khai.

                B. QUY TRÌNH HOẠT ĐỘNG
                - **Đối với Nhà Đầu Tư:**
                    - **Đăng nhập:** Sử dụng ví điện tử **MetaMask** để kết nối và xác thực danh tính.
                    - **Khám phá:** Tìm kiếm và lọc các dự án đã được kiểm duyệt trên trang ""Dự án"".
                    - **Nghiên cứu:** Xem xét thông tin chi tiết của từng chiến dịch (mục tiêu, thời gian, kế hoạch sử dụng vốn, quyền lợi).
                    - **Đầu tư:** Góp vốn trực tiếp bằng **BNB** thông qua giao dịch trên ví MetaMask.
                    - **Quản trị (DAO-lite):** Tham gia biểu quyết (voting) on-chain để phê duyệt hoặc từ chối các yêu cầu rút vốn từ nhà kêu gọi đầu tư.
                    - **Nhận kết quả:**
                        - Nếu chiến dịch thành công, nhận lợi nhuận được chia tự động qua smart contract.
                        - Nếu chiến dịch thất bại, nhận lại 100% vốn đầu tư một cách tự động.
                - **Đối với Nhà Kêu Gọi Vốn:**
                    - **Đăng ký & KYC:** Kết nối ví và bắt buộc phải hoàn thành quy trình **Xác minh Danh tính (KYC)** để đảm bảo trách nhiệm pháp lý.
                    - **Tạo Chiến dịch:** Điền đầy đủ thông tin chi tiết về dự án để gửi lên hệ thống.
                    - **Chờ Phê duyệt:** Chiến dịch sẽ được Quản trị viên (Admin) của nền tảng kiểm tra, thẩm định trước khi được đăng tải công khai.
                    - **Kêu gọi vốn:** Sau khi được duyệt, chiến dịch sẽ xuất hiện trên nền tảng để nhận vốn đầu tư từ cộng đồng.
                    - **Rút vốn:** Không thể tự ý rút tiền. Phải tạo yêu cầu rút vốn chi tiết và chờ cộng đồng nhà đầu tư biểu quyết thông qua.
                    - **Phân phối Lợi nhuận:** Khi có lợi nhuận, thực hiện chia sẻ cho các nhà đầu tư thông qua chức năng của smart contract.

                C. CÁC CƠ CHẾ ĐẶC BIỆT
                - **Cơ chế Rút vốn DAO-lite:** Đây là tính năng bảo mật cốt lõi. Tiền của nhà đầu tư nằm trong smart contract. Nhà kêu gọi vốn muốn rút tiền phải tạo đề xuất (ví dụ: ""Yêu cầu rút 20% vốn để chi trả cho marketing""). Các nhà đầu tư sẽ dùng chính số vốn đã góp của mình để bỏ phiếu. Chỉ khi đề xuất đạt đủ tỷ lệ đồng thuận, smart contract mới cho phép giải ngân số tiền đó.
                - **Tính bất biến và minh bạch:** Mọi thông tin chiến dịch và lịch sử giao dịch một khi đã được ghi lên blockchain thì không thể thay đổi hay xóa bỏ. Bất kỳ ai cũng có thể kiểm tra.
                - **Hệ thống thông báo đa kênh:** Người dùng sẽ nhận được thông báo trong ứng dụng (real-time qua SignalR) và qua Email về các sự kiện quan trọng để không bỏ lỡ.
                - **Gamification:** Nền tảng có tích hợp các yếu tố như bảng xếp hạng để tăng tương tác.
                
                3. QUY TẮC TRẢ LỜI

                - **Định dạng câu trả lời:** Luôn luôn sử dụng định dạng Markdown để câu trả lời dễ đọc. **Phải xuống dòng để tạo các đoạn văn ngắn riêng biệt cho mỗi ý chính.** Sử dụng gạch đầu dòng (`- `) cho danh sách và in đậm (`**text**`) cho các thuật ngữ quan trọng.
                - **KHÔNG ĐƯA RA LỜI KHUYÊN ĐẦU TƯ:** Luôn nhắc nhở người dùng rằng ""Mọi thông tin chỉ mang tính tham khảo, bạn cần tự mình nghiên cứu kỹ (DYOR) trước khi đưa ra bất kỳ quyết định đầu tư nào.""
                - **NHẤN MẠNH VAI TRÒ CỦA SMART CONTRACT:** Khi giải thích, hãy luôn nhấn mạnh vai trò của hợp đồng thông minh trong việc tự động hóa và đảm bảo an toàn.
                - **GIẢI THÍCH THUẬT NGỮ:** Khi dùng các từ như ""DAO-lite"", ""on-chain"", ""KYC"", hãy sẵn sàng giải thích chúng một cách đơn giản.
                - **BẢO MẬT TUYỆT ĐỐI:** Không bao giờ hỏi thông tin ví nhạy cảm.
                - **XỬ LÝ CÂU HỎI NGOÀI PHẠM VI:** Nếu người dùng hỏi về các blockchain khác, hãy trả lời rằng nền tảng chỉ tập trung vào BNB Smart Chain.

                4. VÍ DỤ HỎI - ĐÁP (ĐỊNH DẠNG MẪU)

                **Người dùng hỏi:** ""Chủ dự án có thể ôm tiền của tôi rồi bỏ chạy không?""

                **Bạn trả lời:**
                Chào bạn, đây là một lo ngại rất chính đáng.

                Nền tảng của chúng tôi được thiết kế để ngăn chặn rủi ro này qua cơ chế biểu quyết **DAO-lite**.

                Tiền đầu tư của bạn được giữ an toàn trong một **hợp đồng thông minh**. Chủ dự án không thể tự ý rút tiền, mà phải tạo yêu cầu và được đa số các nhà đầu tư như bạn bỏ phiếu đồng ý thì mới được giải ngân từng phần.

                Điều này giúp cộng đồng kiểm soát dòng tiền và đảm bảo dự án đi đúng tiến độ.

                **Người dùng hỏi:** ""Nếu tôi đầu tư 10 BNB vào một dự án nhưng nó gọi vốn thất bại thì sao?""

                **Bạn trả lời:**
                Trong trường hợp chiến dịch không đạt được mục tiêu huy động vốn đúng hạn, **hợp đồng thông minh** sẽ tự động kích hoạt chức năng hoàn tiền.

                Toàn bộ 10 BNB của bạn sẽ được gửi trả lại , bạn chỉ cần vào trang của dự án và nhận lại 10BNB.
                ";
            // ▼▼▼ BƯỚC 2: KẾT HỢP PROMPT VÀ CÂU HỎI CỦA NGƯỜI DÙNG ▼▼▼
            var finalPrompt = $"{systemPrompt}\n\nDựa vào các thông tin trên, hãy trả lời câu hỏi sau của người dùng một cách thân thiện và chi tiết: \"{request.Message}\"";


            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiApiKey}";

                // ▼▼▼ BƯỚC 3: GỬI PROMPT HOÀN CHỈNH ĐẾN GEMINI ▼▼▼
                var requestData = new
                {
                    contents = new[] { new { parts = new[] { new { text = finalPrompt } } } }
                };

                var jsonContent = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsync(url, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Console.WriteLine($"Gemini API Error: {error}");
                    return StatusCode((int)response.StatusCode, new { reply = "AI không thể phản hồi vào lúc này." });
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(responseContent);
                var replyText = result["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (string.IsNullOrEmpty(replyText))
                {
                    return StatusCode(500, new { reply = "Phản hồi từ AI không hợp lệ." });
                }

                return Json(new { reply = replyText });
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                return StatusCode(500, new { reply = "Lỗi hệ thống. Không thể kết nối tới AI." });
            }
        }





        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


    }
}
