using System.Threading;
using System.Threading.Tasks;
using InvestDapp.Shared.Enums;

namespace InvestDapp.Application.AuthService.Admin;

public interface IRoleManagementService
{
    Task<RoleTransactionResult> GrantRoleAsync(RoleType role, string walletAddress, CancellationToken cancellationToken = default);
    Task<RoleTransactionResult> RevokeRoleAsync(RoleType role, string walletAddress, CancellationToken cancellationToken = default);
    Task<RoleTransactionResult> GrantAdminAsync(string walletAddress, CancellationToken cancellationToken = default);
    Task<RoleTransactionResult> RevokeAdminAsync(string walletAddress, CancellationToken cancellationToken = default);
}

public record RoleTransactionResult(bool Success, string? TransactionHash, string? NormalizedAddress, string? Error);
