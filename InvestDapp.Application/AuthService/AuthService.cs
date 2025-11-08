using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Nethereum.Signer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Application.AuthService
{
    public class AuthService : IAuthService
    {
        private readonly InvestDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _cache;
        private const string CachePrefix = "UserNonce_";
        private static readonly TimeSpan NonceLifetime = TimeSpan.FromMinutes(5);

        public AuthService(InvestDbContext context, IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
        {
            _dbContext = context;
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
        }
        public async Task<bool> GenerateAndStoreNonceAsync(string walletAddress)
        {
            var profile = _dbContext.Users.FirstOrDefault(p => p.WalletAddress == walletAddress);


            if (profile == null)
            {
               return false;
            }

            var nonce = $"Đăng nhập với ví: {walletAddress} lúc {DateTime.UtcNow}";

            profile.Nonce = nonce;
            profile.NonceGeneratedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return true;
        }



        public async Task SignInUser(User user)
        {
            // ✅ Lấy KYC record MỚI NHẤT (theo SubmittedAt) để tránh lấy nhầm record cũ
            var kyc = await _dbContext.FundraiserKyc
                .Where(k => k.UserId == user.ID && k.IsApproved == true)
                .OrderByDescending(k => k.SubmittedAt)
                .FirstOrDefaultAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()),
                new Claim("WalletAddress", user.WalletAddress ?? ""),
                new Claim(ClaimTypes.Name, user.Name ?? ""),
                new Claim(ClaimTypes.Role, user.Role)
            };

            // ✅ Cần cả IsApproved = true VÀ AcceptedTerms = true
            if (kyc != null && kyc.AcceptedTerms)
            {
                claims.Add(new Claim(ClaimTypes.Role, "KycVerified"));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(claimsIdentity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext != null)
            {
                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    authProperties);
            }
        }
        public async Task<bool> VerifySignatureAsync(string walletAddress, string signature, string nonce)
        {
            var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.WalletAddress == walletAddress);

            if (user == null || string.IsNullOrEmpty(user.Nonce) || user.NonceGeneratedAt == null)
                return false;

            // Kiểm tra hết hạn (ví dụ 5 phút)
            if (DateTime.UtcNow - user.NonceGeneratedAt > TimeSpan.FromMinutes(5))
                return false;

            // Dùng Nethereum để xác minh chữ ký
            var signer = new Nethereum.Signer.EthereumMessageSigner();
            var recoveredAddress = signer.EncodeUTF8AndEcRecover(user.Nonce, signature);

            var isValid = recoveredAddress.Equals(walletAddress, StringComparison.OrdinalIgnoreCase);

            if (isValid)
            {
                // ✅ Đăng nhập thành công, xoá hoặc reset nonce để tránh replay
                user.Nonce = null;
                user.NonceGeneratedAt = null;
                await _dbContext.SaveChangesAsync();
            }

            return isValid;
        }

        // ==========================================
        // NEW: User Wallet Authentication with Nonce
        // ==========================================
        
        public async Task<UserNonceResult> GenerateUserNonceAsync(string walletAddress)
        {
            var normalized = NormalizeWallet(walletAddress);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return new UserNonceResult(false, null, "Địa chỉ ví không hợp lệ.");
            }

            // Ensure user exists
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.WalletAddress == normalized);
            if (user == null)
            {
                user = await EnsureUserAsync(normalized);
                if (user == null)
                {
                    return new UserNonceResult(false, null, "Không thể khởi tạo người dùng.");
                }
            }

            // Generate nonce similar to admin login
            var nonce = $"INVESTDAPP|USER|LOGIN|{Guid.NewGuid()}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            _cache.Set(CachePrefix + normalized, nonce, NonceLifetime);

            return new UserNonceResult(true, nonce, null);
        }

        public async Task<UserSignInResult> VerifyUserSignatureAsync(string walletAddress, string signature)
        {
            var normalized = NormalizeWallet(walletAddress);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return new UserSignInResult(false, "Địa chỉ ví không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(signature))
            {
                return new UserSignInResult(false, "Thiếu chữ ký xác thực.");
            }

            // Get nonce from cache
            if (!_cache.TryGetValue(CachePrefix + normalized, out string? nonce) || string.IsNullOrWhiteSpace(nonce))
            {
                return new UserSignInResult(false, "Phiên xác thực đã hết hạn, vui lòng lấy nonce mới.");
            }

            try
            {
                // Verify signature using Nethereum
                var signer = new EthereumMessageSigner();
                var recovered = signer.EncodeUTF8AndEcRecover(nonce, signature);
                
                if (!string.Equals(recovered, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return new UserSignInResult(false, "Chữ ký không khớp với địa chỉ ví.");
                }

                // Get or create user
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.WalletAddress == normalized);
                if (user == null)
                {
                    user = await EnsureUserAsync(normalized);
                    if (user == null)
                    {
                        return new UserSignInResult(false, "Không thể tạo hồ sơ người dùng.");
                    }
                }

                // Sign in user
                await SignInUser(user);

                // Remove nonce from cache to prevent replay attacks
                _cache.Remove(CachePrefix + normalized);

                return new UserSignInResult(true, null);
            }
            catch (Exception ex)
            {
                return new UserSignInResult(false, $"Lỗi xác thực: {ex.Message}");
            }
        }

        private async Task<User?> EnsureUserAsync(string walletAddress)
        {
            var normalized = NormalizeWallet(walletAddress);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.WalletAddress == normalized);
            if (user != null)
            {
                return user;
            }

            user = new User
            {
                WalletAddress = normalized,
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            return user;
        }

        private static string NormalizeWallet(string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
            {
                return string.Empty;
            }

            var trimmed = walletAddress.Trim();
            if (!trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || trimmed.Length != 42)
            {
                return string.Empty;
            }

            return trimmed.ToLowerInvariant();
        }
    }
}
