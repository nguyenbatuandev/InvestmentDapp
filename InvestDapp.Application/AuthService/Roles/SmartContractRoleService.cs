using System.Text;
using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Shared.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.Web3;

namespace InvestDapp.Application.AuthService.Roles;

public class SmartContractRoleService : IRoleService
{
    private readonly Web3 _web3;
    private readonly IMemoryCache _cache;
    private readonly BlockchainConfig _blockchainConfig;
    private readonly ILogger<SmartContractRoleService> _logger;

    private static readonly RoleType[] TrackedRoles =
    {
        RoleType.SuperAdmin,
        RoleType.Admin,
        RoleType.Moderator,
        RoleType.SupportAgent,
        RoleType.Fundraiser
    };

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public SmartContractRoleService(
        Web3 web3,
        IMemoryCache cache,
        IOptions<BlockchainConfig> blockchainConfig,
        ILogger<SmartContractRoleService> logger)
    {
        _web3 = web3;
        _cache = cache;
        _blockchainConfig = blockchainConfig.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<RoleType>> GetRolesAsync(string walletAddress)
    {
        var normalized = NormalizeWallet(walletAddress);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<RoleType>();
        }

        var contractAddress = string.IsNullOrWhiteSpace(_blockchainConfig.RoleManagerContractAddress)
            ? _blockchainConfig.ContractAddress
            : _blockchainConfig.RoleManagerContractAddress;

            if (IsZeroAddress(contractAddress))
            {
                contractAddress = _blockchainConfig.ContractAddress;
            }

        if (string.IsNullOrWhiteSpace(contractAddress) ||
            !contractAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("RoleManagerContractAddress is not configured correctly. Returning empty roles for {Wallet}", normalized);
            return Array.Empty<RoleType>();
        }

        var cacheKey = $"roles::{normalized}";
    if (_cache.TryGetValue(cacheKey, out IReadOnlyCollection<RoleType>? cached) && cached is not null)
        {
            return cached;
        }

        var handler = _web3.Eth.GetContractQueryHandler<HasRoleFunction>();
        var roles = new List<RoleType>();

        foreach (var role in TrackedRoles)
        {
            var roleId = GetRoleIdentifier(role);
            if (roleId is null)
            {
                continue;
            }

            try
            {
                var result = await handler
                    .QueryAsync<bool>(contractAddress, new HasRoleFunction
                    {
                        Role = roleId,
                        Account = normalized
                    })
                    .ConfigureAwait(false);

                _logger.LogInformation("Role check {Role} for {Wallet}: {Result}", role, normalized, result);

                if (result)
                {
                    roles.Add(role);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query role {Role} for wallet {Wallet}", role, normalized);
            }
        }

        var snapshot = (IReadOnlyCollection<RoleType>)roles.AsReadOnly();
        _cache.Set(cacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    public async Task<bool> HasRoleAsync(string walletAddress, RoleType role)
    {
        var roles = await GetRolesAsync(walletAddress).ConfigureAwait(false);
        return roles.Contains(role);
    }

    public void InvalidateRoleCache(string walletAddress)
    {
        var normalized = NormalizeWallet(walletAddress);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var cacheKey = $"roles::{normalized}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Role cache invalidated for {Wallet}", normalized);
    }

    private static string NormalizeWallet(string walletAddress)
    {
        if (string.IsNullOrWhiteSpace(walletAddress))
        {
            return string.Empty;
        }

        if (!walletAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return walletAddress.Trim();
    }

    private static byte[]? GetRoleIdentifier(RoleType role)
    {
        switch (role)
        {
            case RoleType.SuperAdmin:
                return DefaultAdminRole;
            case RoleType.Admin:
                return AdminRole;
            case RoleType.Moderator:
            case RoleType.SupportAgent:
            case RoleType.Fundraiser:
                return null;
            default:
                return null;
        }
    }

    private static readonly byte[] DefaultAdminRole = new byte[32];
    private static readonly byte[] AdminRole = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("ADMIN_ROLE"));

        private static bool IsZeroAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return true;
            }

            var trimmed = address.Trim();
            if (!trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (var i = 2; i < trimmed.Length; i++)
            {
                if (trimmed[i] != '0')
                {
                    return false;
                }
            }

            return true;
        }

    [Function("hasRole", "bool")]
    public class HasRoleFunction : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public byte[] Role { get; set; } = Array.Empty<byte>();

        [Parameter("address", "account", 2)]
        public string Account { get; set; } = string.Empty;
    }
}
