using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models.Kyc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace InvestDapp.Infrastructure.Data.Repository
{
    public class KycRepository : IKyc
    {
        private readonly InvestDbContext _context;
        private readonly ILogger<KycRepository> _logger;

        public KycRepository(InvestDbContext context, ILogger<KycRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FundraiserKyc?> GetLatestFundraiserKycByWalletAsync(string walletAddress)
        {
            var latestFundraiserKyc = await _context.FundraiserKyc
                .Where(f => f.User.WalletAddress == walletAddress)
                .OrderByDescending(f => f.SubmittedAt) 
                .FirstOrDefaultAsync();
            return latestFundraiserKyc;
        }

        public async Task<(IReadOnlyList<FundraiserKyc> Items, int TotalCount)> QueryKycsAsync(string? status, string? accountType, string? searchTerm, int page, int pageSize)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize <= 0)
            {
                pageSize = 10;
            }

            var query = _context.FundraiserKyc
                .Include(x => x.User)
                .Include(x => x.IndividualInfo)
                .Include(x => x.CompanyInfo)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToLower();
                query = normalizedStatus switch
                {
                    "pending" => query.Where(x => x.IsApproved == null),
                    "approved" => query.Where(x => x.IsApproved == true),
                    "rejected" => query.Where(x => x.IsApproved == false),
                    _ => query
                };
            }

            if (!string.IsNullOrWhiteSpace(accountType))
            {
                var normalizedAccountType = accountType.Trim().ToLower();
                query = query.Where(x => x.AccountType.ToLower() == normalizedAccountType);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var trimmed = searchTerm.Trim();
                var likePattern = $"%{trimmed}%";

                query = query.Where(x =>
                    (x.User != null && (
                        (x.User.Name != null && EF.Functions.Like(x.User.Name, likePattern)) ||
                        (x.User.Email != null && EF.Functions.Like(x.User.Email, likePattern)) ||
                        (x.User.WalletAddress != null && EF.Functions.Like(x.User.WalletAddress, likePattern))
                    )) ||
                    (x.ContactEmail != null && EF.Functions.Like(x.ContactEmail, likePattern)) ||
                    (x.IndividualInfo != null && (
                        (x.IndividualInfo.FullName != null && EF.Functions.Like(x.IndividualInfo.FullName, likePattern)) ||
                        (x.IndividualInfo.IdNumber != null && EF.Functions.Like(x.IndividualInfo.IdNumber, likePattern)) ||
                        (x.IndividualInfo.Nationality != null && EF.Functions.Like(x.IndividualInfo.Nationality, likePattern))
                    )) ||
                    (x.CompanyInfo != null && (
                        (x.CompanyInfo.CompanyName != null && EF.Functions.Like(x.CompanyInfo.CompanyName, likePattern)) ||
                        (x.CompanyInfo.RegistrationNumber != null && EF.Functions.Like(x.CompanyInfo.RegistrationNumber, likePattern)) ||
                        (x.CompanyInfo.RegisteredCountry != null && EF.Functions.Like(x.CompanyInfo.RegisteredCountry, likePattern))
                    ))
                );
            }

            var totalCount = await query.CountAsync();
            var skip = (page - 1) * pageSize;

            var items = await query
                .OrderBy(x => x.IsApproved == null ? 0 : x.IsApproved == true ? 1 : 2)
                .ThenByDescending(x => x.SubmittedAt)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<FundraiserKyc?> GetKycByIdAsync(int id)
        {
            return await _context.FundraiserKyc
                .Include(x => x.User)
                .Include(x => x.IndividualInfo)
                .Include(x => x.CompanyInfo)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<bool> UpdateKycStatusAsync(int id, bool? isApproved)
        {
            var kyc = await _context.FundraiserKyc.FirstOrDefaultAsync(x => x.Id == id);
            if (kyc == null)
            {
                return false;
            }

            kyc.IsApproved = isApproved;
            _context.FundraiserKyc.Update(kyc);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<FundraiserKyc> SubmitKycAsync(FundraiserKycRequest model, int id)
        {
            try
            {
              

                // 3. Tạo đối tượng FundraiserKyc chính
                var kyc = new FundraiserKyc
                {
                    AccountType = model.AccountType,
                    ContactEmail = model.ContactEmail,
                    AcceptedTerms = model.AcceptedTerms,
                    WebsiteOrLinkedIn = model.WebsiteOrLinkedIn,
                    IsApproved = null, // Trạng thái chờ duyệt
                    UserId = id,
                    SubmittedAt = DateTime.UtcNow
                };

                var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadRoot);

                // 4. Xử lý thông tin và file upload tùy theo loại tài khoản
                if (model.AccountType == "individual")
                {
                    // Tạo đối tượng IndividualKycInfo và gán thông tin
                    kyc.IndividualInfo = new IndividualKycInfo
                    {
                        FullName = model.FullName,
                        IdNumber = model.IdNumber,
                        Nationality = model.Nationality, // THÊM MỚI
                        // Lưu file và lấy đường dẫn
                        IdFrontImagePath = await SaveFileAsync(model.IdFrontImage, uploadRoot),
                        SelfieWithIdPath = await SaveFileAsync(model.SelfieWithIdImage, uploadRoot) // THÊM MỚI
                    };
                }
                else if (model.AccountType == "company")
                {
                    // Tạo đối tượng CompanyKycInfo và gán thông tin
                    kyc.CompanyInfo = new CompanyKycInfo
                    {
                        CompanyName = model.CompanyName,
                        RegistrationNumber = model.RegistrationNumber,
                        RegisteredCountry = model.RegisteredCountry, // THÊM MỚI
                        // Lưu file và lấy đường dẫn
                        BusinessLicensePdfPath = await SaveFileAsync(model.BusinessLicensePdf, uploadRoot),
                        DirectorIdImagePath = await SaveFileAsync(model.DirectorIdImage, uploadRoot) // THÊM MỚI
                    };
                }

                // 5. Thêm vào context và lưu vào database
                _context.FundraiserKyc.Add(kyc);
                await _context.SaveChangesAsync();

                return kyc;
            }
            catch (InvalidOperationException ex)
            {

                throw;
            }
        }

        /// <summary>
        /// Hàm trợ giúp để lưu file vào thư mục và trả về đường dẫn tương đối.
        /// </summary>
        /// <param name="file">File được tải lên từ form (IFormFile).</param>
        /// <param name="uploadRootPath">Đường dẫn tuyệt đối đến thư mục uploads.</param>
        /// <returns>Đường dẫn tương đối để lưu vào database (ví dụ: /uploads/tenfile.jpg).</returns>
        private async Task<string> SaveFileAsync(IFormFile file, string uploadRootPath)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            // Tạo tên file duy nhất để tránh trùng lặp
            string uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            string absoluteFilePath = Path.Combine(uploadRootPath, uniqueFileName);

            // Lưu file vào server
            using (var stream = new FileStream(absoluteFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Trả về đường dẫn tương đối để sử dụng trong HTML/CSS
            return $"/uploads/{uniqueFileName}";
        }
    }
}