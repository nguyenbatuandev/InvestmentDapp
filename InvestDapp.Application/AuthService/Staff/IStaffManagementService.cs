using System.Collections.Generic;
using System.Threading.Tasks;
using InvestDapp.Shared.DTOs;
using InvestDapp.Shared.Enums;

namespace InvestDapp.Application.AuthService.Staff
{
    public interface IStaffManagementService
    {
        /// <summary>
        /// Get all staff members
        /// </summary>
        Task<List<StaffDTO>> GetAllStaffAsync();

        /// <summary>
        /// Get staff by ID
        /// </summary>
        Task<StaffDTO?> GetStaffByIdAsync(int staffId);

        /// <summary>
        /// Get staff by wallet address
        /// </summary>
        Task<StaffDTO?> GetStaffByWalletAsync(string walletAddress);

        /// <summary>
        /// Create new staff member
        /// </summary>
        Task<(bool Success, string Message, StaffDTO? Staff)> CreateStaffAsync(
            CreateStaffRequest request, string createdBy);

        /// <summary>
        /// Grant a role to staff member
        /// </summary>
        Task<(bool Success, string Message)> GrantRoleAsync(
            int staffId, RoleType role, string grantedBy);

        /// <summary>
        /// Revoke a role from staff member
        /// </summary>
        Task<(bool Success, string Message)> RevokeRoleAsync(
            int staffId, RoleType role);

        /// <summary>
        /// Update staff active status
        /// </summary>
        Task<(bool Success, string Message)> UpdateStaffStatusAsync(
            int staffId, bool isActive);

        /// <summary>
        /// Delete staff (soft delete)
        /// </summary>
        Task<(bool Success, string Message)> DeleteStaffAsync(int staffId);

        /// <summary>
        /// Get roles for authentication (used during login)
        /// </summary>
        Task<List<RoleType>> GetStaffRolesForAuthAsync(string walletAddress);
    }
}
