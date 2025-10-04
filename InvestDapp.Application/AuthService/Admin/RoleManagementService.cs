using System.Text;
using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Shared.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace InvestDapp.Application.AuthService.Admin;

public class RoleManagementService : IRoleManagementService
{
    private readonly BlockchainConfig _config;
    private readonly ILogger<RoleManagementService> _logger;

    private static readonly byte[] DefaultAdminRole = new byte[32];
    private static readonly byte[] AdminRole = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("ADMIN_ROLE"));
    private static readonly byte[] CreatorRole = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("CREATOR_ROLE"));

    public RoleManagementService(IOptions<BlockchainConfig> config, ILogger<RoleManagementService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public Task<RoleTransactionResult> GrantRoleAsync(RoleType role, string walletAddress, CancellationToken cancellationToken = default)
    {
        if (role == RoleType.Admin)
        {
            return ExecuteRoleMutationAsync<GrantAdminFunction>(walletAddress, cancellationToken, (function, checksum) =>
            {
                function.User = checksum;
            });
        }

        if (!TryResolveRole(role, out var roleIdentifier, out var resolveError))
        {
            return Task.FromResult(new RoleTransactionResult(false, null, null, resolveError));
        }

        return ExecuteRoleMutationAsync<GrantRoleFunction>(walletAddress, cancellationToken, (function, checksum) =>
        {
            function.Role = roleIdentifier;
            function.Account = checksum;
        });
    }

    public Task<RoleTransactionResult> RevokeRoleAsync(RoleType role, string walletAddress, CancellationToken cancellationToken = default)
    {
        if (role == RoleType.Admin)
        {
            return ExecuteRoleMutationAsync<RevokeAdminFunction>(walletAddress, cancellationToken, (function, checksum) =>
            {
                function.User = checksum;
            });
        }

        if (!TryResolveRole(role, out var roleIdentifier, out var resolveError))
        {
            return Task.FromResult(new RoleTransactionResult(false, null, null, resolveError));
        }

        return ExecuteRoleMutationAsync<RevokeRoleFunction>(walletAddress, cancellationToken, (function, checksum) =>
        {
            function.Role = roleIdentifier;
            function.Account = checksum;
        });
    }

    public Task<RoleTransactionResult> GrantAdminAsync(string walletAddress, CancellationToken cancellationToken = default)
    {
        return GrantRoleAsync(RoleType.Admin, walletAddress, cancellationToken);
    }

    public Task<RoleTransactionResult> RevokeAdminAsync(string walletAddress, CancellationToken cancellationToken = default)
    {
        return RevokeRoleAsync(RoleType.Admin, walletAddress, cancellationToken);
    }

    private async Task<RoleTransactionResult> ExecuteRoleMutationAsync<TFunction>(
        string walletAddress,
        CancellationToken cancellationToken,
        Action<TFunction, string> configure)
        where TFunction : FunctionMessage, new()
    {
        if (!TryNormalizeWallet(walletAddress, out var checksum, out var validationError))
        {
            return new RoleTransactionResult(false, null, null, validationError);
        }

        if (!TryGetContractAddress(out var contractAddress, out var contractError))
        {
            return new RoleTransactionResult(false, null, checksum, contractError);
        }

        if (!TryCreateAccount(out var account, out var accountError))
        {
            return new RoleTransactionResult(false, null, checksum, accountError);
        }

        var adminAccount = account!;

        try
        {
            var web3 = new Web3(adminAccount, _config.RpcUrl);
            var handler = web3.Eth.GetContractTransactionHandler<TFunction>();
            var function = new TFunction
            {
                FromAddress = adminAccount.Address
            };

            configure(function, checksum);

            TransactionReceipt receipt = await handler
                .SendRequestAndWaitForReceiptAsync(contractAddress, function, cancellationToken)
                .ConfigureAwait(false);

            return new RoleTransactionResult(true, receipt.TransactionHash, checksum, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute role mutation {Function} for {Wallet}", typeof(TFunction).Name, checksum);
            var message = ExtractFriendlyError(ex);
            return new RoleTransactionResult(false, null, checksum, message);
        }
    }

    private bool TryNormalizeWallet(string walletAddress, out string checksumAddress, out string? error)
    {
        checksumAddress = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(walletAddress))
        {
            error = "Địa chỉ ví không được bỏ trống.";
            return false;
        }

        var trimmed = walletAddress.Trim();
        var addressUtil = new AddressUtil();
        if (!addressUtil.IsValidEthereumAddressHexFormat(trimmed))
        {
            error = "Địa chỉ ví không hợp lệ.";
            return false;
        }

        checksumAddress = addressUtil.ConvertToChecksumAddress(trimmed);
        return true;
    }

    private bool TryGetContractAddress(out string contractAddress, out string? error)
    {
        error = null;
        contractAddress = string.Empty;

        var candidate = !string.IsNullOrWhiteSpace(_config.RoleManagerContractAddress)
            ? _config.RoleManagerContractAddress
            : _config.ContractAddress;

        if (IsZeroAddress(candidate))
        {
            candidate = _config.ContractAddress;
        }

        var addressUtil = new AddressUtil();
        if (!addressUtil.IsValidEthereumAddressHexFormat(candidate) || IsZeroAddress(candidate))
        {
            error = "Chưa cấu hình địa chỉ smart contract quản lý quyền hợp lệ.";
            return false;
        }

        contractAddress = addressUtil.ConvertToChecksumAddress(candidate);
        return true;
    }

    private static bool IsZeroAddress(string? address)
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

    private bool TryCreateAccount(out Account? account, out string? error)
    {
        account = null;
        error = null;

        if (string.IsNullOrWhiteSpace(_config.DefaultAdminPrivateKey))
        {
            error = "Chưa cấu hình private key cho tài khoản quản trị mặc định.";
            return false;
        }

        try
        {
            account = new Account(_config.DefaultAdminPrivateKey, _config.ChainId);
            if (!string.IsNullOrWhiteSpace(_config.DefaultAdminAddress))
            {
                var addressUtil = new AddressUtil();
                var expected = addressUtil.ConvertToChecksumAddress(_config.DefaultAdminAddress);
                var actual = addressUtil.ConvertToChecksumAddress(account.Address);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Configured admin address does not match derived account address. Expected {Expected} but got {Actual}", expected, actual);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create blockchain account from configured private key");
            error = "Private key không hợp lệ hoặc không thể khởi tạo tài khoản.";
            return false;
        }
    }

    private bool TryResolveRole(RoleType role, out byte[] identifier, out string? error)
    {
        identifier = Array.Empty<byte>();
        error = null;

        switch (role)
        {
            case RoleType.SuperAdmin:
                identifier = DefaultAdminRole;
                return true;
            case RoleType.Admin:
                identifier = AdminRole;
                return true;
            case RoleType.Fundraiser:
                identifier = CreatorRole;
                return true;
            default:
                error = "Vai trò này hiện không thể quản lý trực tiếp từ dashboard.";
                return false;
        }
    }

    private static string ExtractFriendlyError(Exception ex)
    {
        var message = ex.Message;
        if (ex.InnerException != null)
        {
            message = ex.InnerException.Message;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Giao dịch thất bại.";
        }

        return message;
    }

    [Function("grantAdmin")]
    public class GrantAdminFunction : FunctionMessage
    {
        [Parameter("address", "_user", 1)]
        public string User { get; set; } = string.Empty;
    }

    [Function("revokeAdmin")]
    public class RevokeAdminFunction : FunctionMessage
    {
        [Parameter("address", "_user", 1)]
        public string User { get; set; } = string.Empty;
    }

    [Function("grantRole")]
    public class GrantRoleFunction : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public byte[] Role { get; set; } = Array.Empty<byte>();

        [Parameter("address", "account", 2)]
        public string Account { get; set; } = string.Empty;
    }

    [Function("revokeRole")]
    public class RevokeRoleFunction : FunctionMessage
    {
        [Parameter("bytes32", "role", 1)]
        public byte[] Role { get; set; } = Array.Empty<byte>();

        [Parameter("address", "account", 2)]
        public string Account { get; set; } = string.Empty;
    }
}
