using System.Linq;
using System.Security.Claims;
using InvestDapp.Application.AuthService.Roles;
using InvestDapp.Application.AuthService.Staff;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Nethereum.Signer;

namespace InvestDapp.Application.AuthService.Admin;

public class AdminLoginService : IAdminLoginService
{
    private const string CachePrefix = "admin-login-nonce::";
    private static readonly TimeSpan NonceLifetime = TimeSpan.FromMinutes(5);
    // FIXED: Allow SupportAgent, Moderator, and Fundraiser to access admin panel
    private static readonly RoleType[] AdminRoles = { 
        RoleType.SuperAdmin, 
        RoleType.Admin, 
        RoleType.Moderator, 
        RoleType.SupportAgent, 
        RoleType.Fundraiser 
    };
    private static readonly IReadOnlyDictionary<RoleType, string[]> RoleClaimAliases = new Dictionary<RoleType, string[]>
    {
        [RoleType.SuperAdmin] = new[] { "SuperAdmin", "Admin" },
        [RoleType.Admin] = new[] { "Admin" },
        [RoleType.Moderator] = new[] { "Moderator" },
        [RoleType.SupportAgent] = new[] { "SupportAgent", "Support" },
        [RoleType.Fundraiser] = new[] { "Fundraiser" },
        [RoleType.User] = new[] { "User", "Investor" }
    };

    private readonly IMemoryCache _cache;
    private readonly IRoleService _roleService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly InvestDbContext _dbContext;
    private readonly IStaffManagementService _staffService;
    private readonly ILogger<AdminLoginService> _logger;

    public AdminLoginService(
        IMemoryCache cache,
        IRoleService roleService,
        IHttpContextAccessor httpContextAccessor,
        InvestDbContext dbContext,
        IStaffManagementService staffService,
        ILogger<AdminLoginService> logger)
    {
        _cache = cache;
        _roleService = roleService;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _staffService = staffService;
        _logger = logger;
    }

    public Task<AdminNonceResult> GenerateNonceAsync(string walletAddress)
    {
        var normalized = NormalizeWallet(walletAddress);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Task.FromResult(new AdminNonceResult(false, null, "Địa chỉ ví không hợp lệ."));
        }

        var nonce = $"INVESTDAPP|LOGIN|{Guid.NewGuid()}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        _cache.Set(CachePrefix + normalized, nonce, NonceLifetime);
        return Task.FromResult(new AdminNonceResult(true, nonce, null));
    }

    public async Task<AdminSignInResult> SignInWithSignatureAsync(string walletAddress, string signature)
    {
        var normalized = NormalizeWallet(walletAddress);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new AdminSignInResult(false, null, "Địa chỉ ví không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return new AdminSignInResult(false, null, "Thiếu chữ ký xác thực.");
        }

        if (!_cache.TryGetValue(CachePrefix + normalized, out string? nonce) || string.IsNullOrWhiteSpace(nonce))
        {
            return new AdminSignInResult(false, null, "Phiên xác thực đã hết hạn, vui lòng lấy nonce mới.");
        }

        try
        {
            var signer = new EthereumMessageSigner();
            var recovered = signer.EncodeUTF8AndEcRecover(nonce, signature);
            if (!string.Equals(recovered, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Signature verification failed for wallet {Wallet}", normalized);
                return new AdminSignInResult(false, null, "Chữ ký không hợp lệ.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover address for admin login");
            return new AdminSignInResult(false, null, "Không thể xác minh chữ ký: " + ex.Message);
        }

        var blockchainRoles = await _roleService.GetRolesAsync(normalized).ConfigureAwait(false);
        var roleSet = new HashSet<RoleType>(blockchainRoles);

        var offchainRoles = await GetOffchainRolesAsync(normalized).ConfigureAwait(false);

        foreach (var role in offchainRoles)
        {
            roleSet.Add(role);
        }

        if (!roleSet.Intersect(AdminRoles).Any())
        {
            _logger.LogWarning("Access denied for wallet {Wallet} - no admin roles found", normalized);
            return new AdminSignInResult(false, null, "Bạn không có quyền truy cập khu vực quản trị.");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, normalized),
            new Claim("WalletAddress", normalized),
            new Claim(ClaimTypes.Name, normalized),
            new Claim(AuthorizationPolicies.AdminSessionClaim, AuthorizationPolicies.AdminSessionVerified)
        };

        var addedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roleSet)
        {
            if (RoleClaimAliases.TryGetValue(role, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    if (addedRoles.Add(alias))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, alias));
                    }
                }
            }
            else if (addedRoles.Add(role.ToString()))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            }
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
        };

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _logger.LogError("HTTP context is unavailable during admin sign-in");
            return new AdminSignInResult(false, null, "Không thể hoàn tất đăng nhập.");
        }

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties)
            .ConfigureAwait(false);

        _cache.Remove(CachePrefix + normalized);

        var redirectUrl = "/admin/dashboard";
        _logger.LogInformation("Admin login successful for wallet {Wallet}", normalized);
        return new AdminSignInResult(true, redirectUrl, null);
    }

    private async Task<IReadOnlyCollection<RoleType>> GetOffchainRolesAsync(string normalizedWallet)
    {
        // Use new Staff table instead of Users table
        return await _staffService.GetStaffRolesForAuthAsync(normalizedWallet);
    }

    public async Task SignOutAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
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

        return trimmed;
    }
}
