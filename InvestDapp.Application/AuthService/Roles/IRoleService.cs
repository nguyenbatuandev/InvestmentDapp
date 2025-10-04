using InvestDapp.Shared.Enums;

namespace InvestDapp.Application.AuthService.Roles;

public interface IRoleService
{
    Task<IReadOnlyCollection<RoleType>> GetRolesAsync(string walletAddress);
    Task<bool> HasRoleAsync(string walletAddress, RoleType role);
    void InvalidateRoleCache(string walletAddress);
}
