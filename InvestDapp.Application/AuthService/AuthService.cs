using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        public AuthService(InvestDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = context;
            _httpContextAccessor = httpContextAccessor;
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
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()),
                new Claim("WalletAddress", user.WalletAddress),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(claimsIdentity); // 🔧 Sửa: tạo principal

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            var httpContext = _httpContextAccessor.HttpContext; // 🔧 Lấy context đúng cách

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
    }
}
