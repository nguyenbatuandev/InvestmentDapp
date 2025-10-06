using InvestDapp.Application.CampaignService;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Models;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Enums;
using InvestDapp.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        private static readonly CultureInfo VietnameseCulture = new CultureInfo("vi-VN");
        private const int MaxPostsPerCampaign = 3;
        private const int MaxTransactionsPerCampaign = 5;
        private const int MaxRefundsPerCampaign = 3;
        private const int MaxProfitClaimsPerCampaign = 3;

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

        private async Task<string> BuildInvestorDataContextAsync(string walletAddress)
        {
            var normalizedWallet = walletAddress.Trim();
            var walletLower = normalizedWallet.ToLowerInvariant();

            var builder = new StringBuilder();
            builder.AppendLine($"Ví đang hoạt động: {normalizedWallet}");

            var investments = await _dbContext.Investment
                .AsNoTracking()
                .Include(i => i.Campaign)
                .Where(i => i.InvestorAddress.ToLower() == walletLower)
                .OrderByDescending(i => i.Timestamp)
                .ToListAsync();

            var profitClaims = await _dbContext.ProfitClaims
                .AsNoTracking()
                .Include(pc => pc.Profit)
                .ThenInclude(p => p.Campaign)
                .Where(pc => pc.ClaimerWallet.ToLower() == walletLower)
                .OrderByDescending(pc => pc.ClaimedAt)
                .ToListAsync();

            var refundRecords = await _dbContext.Refunds
                .AsNoTracking()
                .Where(r => r.InvestorAddress.ToLower() == walletLower)
                .ToListAsync();

            if (investments.Count == 0 && profitClaims.Count == 0 && refundRecords.Count == 0)
            {
                builder.AppendLine("- Nhà đầu tư chưa có dữ liệu giao dịch nào trên nền tảng.");
                return builder.ToString();
            }

            var campaignIds = new HashSet<int>(investments.Select(i => i.CampaignId));
            foreach (var claim in profitClaims)
            {
                if (claim.Profit?.CampaignId is int campaignId)
                {
                    campaignIds.Add(campaignId);
                }
            }

            foreach (var refund in refundRecords)
            {
                campaignIds.Add(refund.CampaignId);
            }

            var campaignLookup = campaignIds.Count > 0
                ? await _dbContext.Campaigns
                    .AsNoTracking()
                    .Where(c => campaignIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id)
                : new Dictionary<int, Campaign>();

            var approvedPosts = campaignIds.Count > 0
                ? await _dbContext.CampaignPosts
                    .AsNoTracking()
                    .Where(p => campaignIds.Contains(p.CampaignId) && p.ApprovalStatus == ApprovalStatus.Approved)
                    .OrderByDescending(p => p.ApprovedAt ?? p.CreatedAt)
                    .ToListAsync()
                : new List<InvestDapp.Shared.Models.CampaignPost>();

            var totalInvested = investments.Sum(i => (decimal)i.Amount);
            var totalRefunded = refundRecords.Sum(r => BlockchainAmountConverter.ToBnb(r.AmountInWei));
            var totalProfitClaimed = profitClaims.Sum(pc => pc.Amount);
            var investedCampaignCount = investments.Select(i => i.CampaignId).Distinct().Count();

            if (investments.Count > 0)
            {
                builder.AppendLine($"- Tổng số chiến dịch đã đầu tư: {investedCampaignCount}");
                builder.AppendLine($"- Tổng vốn đã góp: {FormatBnbAmount(totalInvested)}");
            }
            else
            {
                builder.AppendLine("- Chưa ghi nhận khoản đầu tư trực tiếp nào.");
            }

            if (refundRecords.Count > 0)
            {
                builder.AppendLine($"- Đã nhận hoàn vốn: {FormatBnbAmount(totalRefunded)} (từ {refundRecords.Select(r => r.CampaignId).Distinct().Count()} chiến dịch)");
            }

            if (profitClaims.Count > 0)
            {
                builder.AppendLine($"- Đã nhận lợi nhuận: {FormatBnbAmount(totalProfitClaimed)} (trong {profitClaims.Count} lượt claim)");
            }

            builder.AppendLine();

            var campaignGroups = investments
                .GroupBy(i => i.CampaignId)
                .OrderByDescending(g => g.Max(i => i.Timestamp))
                .ToList();

            foreach (var campaignGroup in campaignGroups)
            {
                var campaignId = campaignGroup.Key;
                campaignLookup.TryGetValue(campaignId, out var campaign);
                campaign ??= campaignGroup.FirstOrDefault()?.Campaign;

                if (campaign == null)
                {
                    continue;
                }

                builder.AppendLine($"- Chiến dịch: \"{campaign.Name}\" (ID #{campaign.Id})");
                builder.AppendLine($"  • Trạng thái: {DescribeCampaignStatus(campaign.Status)}");
                builder.AppendLine($"  • Thời hạn kết thúc: {FormatDateTime(campaign.EndTime)}");
                builder.AppendLine($"  • Mục tiêu: {FormatBnbAmount((decimal)campaign.GoalAmount)} | Đã huy động: {FormatBnbAmount((decimal)campaign.CurrentRaisedAmount)} (tiến độ {FormatProgress(campaign.CurrentRaisedAmount, campaign.GoalAmount)})");

                var totalByCampaign = campaignGroup.Sum(i => (decimal)i.Amount);
                var latestInvestment = campaignGroup.OrderByDescending(i => i.Timestamp).First();
                builder.AppendLine($"  • Tổng vốn bạn đã góp: {FormatBnbAmount(totalByCampaign)} (giao dịch gần nhất {FormatDateTime(latestInvestment.Timestamp)})");

                var recentInvestments = campaignGroup
                    .OrderByDescending(i => i.Timestamp)
                    .Take(MaxTransactionsPerCampaign)
                    .ToList();

                if (recentInvestments.Count > 0)
                {
                    builder.AppendLine("  • Các giao dịch đầu tư gần nhất:");
                    foreach (var investment in recentInvestments)
                    {
                        builder.AppendLine($"    ◦ {FormatDateTime(investment.Timestamp)} - {FormatBnbAmount((decimal)investment.Amount)} (Tx: {ShortenHash(investment.TransactionHash)})");
                    }
                }

                var latestPosts = approvedPosts
                    .Where(p => p.CampaignId == campaignId)
                    .OrderByDescending(p => p.ApprovedAt ?? p.CreatedAt)
                    .Take(MaxPostsPerCampaign)
                    .ToList();

                if (latestPosts.Count > 0)
                {
                    builder.AppendLine("  • Bài viết cập nhật gần nhất:");
                    foreach (var post in latestPosts)
                    {
                        var publishedAt = post.ApprovedAt ?? post.CreatedAt;
                        builder.AppendLine($"    ◦ [{FormatDateTime(publishedAt)}] ({DescribePostType(post.PostType)}) {post.Title}");
                    }
                }

                var campaignRefunds = refundRecords
                    .Where(r => r.CampaignId == campaignId)
                    .OrderByDescending(r => r.ClaimedAt ?? DateTime.MinValue)
                    .Take(MaxRefundsPerCampaign)
                    .ToList();

                if (campaignRefunds.Count > 0)
                {
                    builder.AppendLine("  • Hoàn vốn liên quan:");
                    foreach (var refund in campaignRefunds)
                    {
                        var refundAmount = BlockchainAmountConverter.ToBnb(refund.AmountInWei);
                        var claimedLabel = refund.ClaimedAt.HasValue
                            ? $"nhận {FormatDateTime(refund.ClaimedAt)}"
                            : "chưa nhận";
                        builder.AppendLine($"    ◦ {FormatBnbAmount(refundAmount)} ({claimedLabel}) Tx: {ShortenHash(refund.TransactionHash)}");
                    }
                }

                var campaignProfitClaims = profitClaims
                    .Where(pc => pc.Profit?.CampaignId == campaignId)
                    .OrderByDescending(pc => pc.ClaimedAt)
                    .Take(MaxProfitClaimsPerCampaign)
                    .ToList();

                if (campaignProfitClaims.Count > 0)
                {
                    builder.AppendLine("  • Lợi nhuận đã nhận:");
                    foreach (var claim in campaignProfitClaims)
                    {
                        builder.AppendLine($"    ◦ {FormatBnbAmount(claim.Amount)} (ngày {FormatDateTime(claim.ClaimedAt)}) Tx: {ShortenHash(claim.TransactionHash)}");
                    }
                }

                builder.AppendLine();
            }

            var investedCampaignIds = new HashSet<int>(campaignGroups.Select(g => g.Key));
            var profitOnlyGroups = profitClaims
                .Where(pc => pc.Profit?.CampaignId != null && !investedCampaignIds.Contains(pc.Profit.CampaignId))
                .GroupBy(pc => pc.Profit!.CampaignId)
                .ToList();

            if (profitOnlyGroups.Count > 0)
            {
                builder.AppendLine("LỢI NHUẬN TỪ CÁC CHIẾN DỊCH KHÁC:");
                foreach (var group in profitOnlyGroups)
                {
                    var campaignId = group.Key;
                    campaignLookup.TryGetValue(campaignId, out var campaign);
                    var campaignLabel = campaign != null
                        ? $"Chiến dịch \"{campaign.Name}\" (ID #{campaign.Id})"
                        : $"Chiến dịch ID #{campaignId}";

                    builder.AppendLine($"- {campaignLabel}");
                    builder.AppendLine($"  • Tổng lợi nhuận đã nhận: {FormatBnbAmount(group.Sum(claim => claim.Amount))}");

                    foreach (var claim in group.OrderByDescending(claim => claim.ClaimedAt).Take(MaxProfitClaimsPerCampaign))
                    {
                        builder.AppendLine($"    ◦ {FormatBnbAmount(claim.Amount)} vào {FormatDateTime(claim.ClaimedAt)} (Tx: {ShortenHash(claim.TransactionHash)})");
                    }

                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private static string FormatProgress(double raised, double goal)
        {
            if (goal <= 0)
            {
                return "0%";
            }

            var ratio = raised / goal;
            if (!double.IsFinite(ratio))
            {
                ratio = 0d;
            }

            if (ratio < 0)
            {
                ratio = 0;
            }

            return ratio.ToString("P1", VietnameseCulture);
        }

        private static string DescribeCampaignStatus(CampaignStatus status)
        {
            return status switch
            {
                CampaignStatus.Draft => "Bản nháp",
                CampaignStatus.PendingPost => "Chờ đăng bài giới thiệu",
                CampaignStatus.PendingApproval => "Chờ quản trị viên phê duyệt",
                CampaignStatus.Active => "Đang huy động vốn",
                CampaignStatus.Voting => "Đang biểu quyết DAO-lite",
                CampaignStatus.Completed => "Đã hoàn thành",
                CampaignStatus.Failed => "Thất bại hoặc bị từ chối",
                _ => status.ToString()
            };
        }

        private static string DescribePostType(PostType postType)
        {
            return postType switch
            {
                PostType.Introduction => "Giới thiệu",
                PostType.Update => "Cập nhật",
                PostType.Achievement => "Thành tựu",
                PostType.Announcement => "Thông báo",
                _ => postType.ToString()
            };
        }

        private static string FormatBnbAmount(decimal amount)
        {
            return $"{BlockchainAmountConverter.FormatBnb(amount)} BNB";
        }

        private static string FormatDateTime(DateTime value)
        {
            DateTime localTime = value.Kind switch
            {
                DateTimeKind.Utc => value.ToLocalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
                _ => value
            };

            return localTime.ToString("dd/MM/yyyy HH:mm", VietnameseCulture);
        }

        private static string FormatDateTime(DateTime? value)
        {
            return value.HasValue ? FormatDateTime(value.Value) : "Chưa cập nhật";
        }

        private static string ShortenHash(string? hash, int visibleChars = 6)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return "Không có";
            }

            var trimmed = hash.Trim();
            if (trimmed.Length <= visibleChars * 2 + 3)
            {
                return trimmed;
            }

            return $"{trimmed[..visibleChars]}…{trimmed[^visibleChars..]}";
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
            var walletAddress = User?.FindFirst("WalletAddress")?.Value?.Trim();
            var investorContext = string.Empty;

            if (!string.IsNullOrWhiteSpace(walletAddress))
            {
                investorContext = await BuildInvestorDataContextAsync(walletAddress);
            }

               // ▼▼▼ BƯỚC 2: KẾT HỢP PROMPT, DỮ LIỆU NGƯỜI DÙNG VÀ CÂU HỎI ▼▼▼
            var finalPromptBuilder = new StringBuilder();
            finalPromptBuilder.AppendLine(systemPrompt);

            if (!string.IsNullOrWhiteSpace(investorContext))
            {
                finalPromptBuilder.AppendLine();
                finalPromptBuilder.AppendLine("THÔNG TIN THỰC TẾ VỀ NHÀ ĐẦU TƯ (CHỈ SỬ DỤNG ĐỂ PHÂN TÍCH):");
                finalPromptBuilder.AppendLine(investorContext);
            }

            finalPromptBuilder.AppendLine();
            finalPromptBuilder.AppendLine($"Dựa vào các thông tin trên, hãy trả lời câu hỏi sau của người dùng một cách thân thiện và chi tiết: \"{request.Message}\"");

            var finalPrompt = finalPromptBuilder.ToString();
            var loadedKey = Environment.GetEnvironmentVariable("GeminiApiKey");

            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={loadedKey}";

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
