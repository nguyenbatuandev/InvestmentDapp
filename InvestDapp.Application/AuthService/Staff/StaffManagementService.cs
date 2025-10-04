using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.DTOs;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvestDapp.Application.AuthService.Staff
{
    public class StaffManagementService : IStaffManagementService
    {
        private readonly InvestDbContext _dbContext;
        private readonly ILogger<StaffManagementService> _logger;

        public StaffManagementService(
            InvestDbContext dbContext,
            ILogger<StaffManagementService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<List<StaffDTO>> GetAllStaffAsync()
        {
            var staff = await _dbContext.Staff
                .Include(s => s.StaffRoles)
                .Where(s => s.DeletedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return staff.Select(s => new StaffDTO
            {
                Id = s.Id,
                WalletAddress = s.WalletAddress,
                Name = s.Name,
                Email = s.Email,
                Avatar = s.Avatar,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                Roles = s.StaffRoles.Select(sr => sr.Role).ToList()
            }).ToList();
        }

        public async Task<StaffDTO?> GetStaffByIdAsync(int staffId)
        {
            var staff = await _dbContext.Staff
                .Include(s => s.StaffRoles)
                .FirstOrDefaultAsync(s => s.Id == staffId && s.DeletedAt == null);

            if (staff == null)
                return null;

            return new StaffDTO
            {
                Id = staff.Id,
                WalletAddress = staff.WalletAddress,
                Name = staff.Name,
                Email = staff.Email,
                Avatar = staff.Avatar,
                IsActive = staff.IsActive,
                CreatedAt = staff.CreatedAt,
                UpdatedAt = staff.UpdatedAt,
                Roles = staff.StaffRoles.Select(sr => sr.Role).ToList()
            };
        }

        public async Task<StaffDTO?> GetStaffByWalletAsync(string walletAddress)
        {
            var normalized = walletAddress.Trim().ToLowerInvariant();

            var staff = await _dbContext.Staff
                .Include(s => s.StaffRoles)
                .FirstOrDefaultAsync(s => s.WalletAddress.ToLower() == normalized && s.DeletedAt == null);

            if (staff == null)
                return null;

            return new StaffDTO
            {
                Id = staff.Id,
                WalletAddress = staff.WalletAddress,
                Name = staff.Name,
                Email = staff.Email,
                Avatar = staff.Avatar,
                IsActive = staff.IsActive,
                CreatedAt = staff.CreatedAt,
                UpdatedAt = staff.UpdatedAt,
                Roles = staff.StaffRoles.Select(sr => sr.Role).ToList()
            };
        }

        public async Task<(bool Success, string Message, StaffDTO? Staff)> CreateStaffAsync(
            CreateStaffRequest request, string createdBy)
        {
            try
            {
                var normalized = request.WalletAddress.Trim().ToLowerInvariant();

                // Check if wallet already exists
                var existing = await _dbContext.Staff
                    .FirstOrDefaultAsync(s => s.WalletAddress.ToLower() == normalized && s.DeletedAt == null);

                if (existing != null)
                {
                    return (false, "Địa chỉ ví này đã tồn tại trong hệ thống", null);
                }

                // Check email uniqueness
                var emailExists = await _dbContext.Staff
                    .AnyAsync(s => s.Email.ToLower() == request.Email.ToLower() && s.DeletedAt == null);

                if (emailExists)
                {
                    return (false, "Email này đã được sử dụng", null);
                }

                var staff = new InvestDapp.Shared.Models.Staff
                {
                    WalletAddress = normalized,
                    Name = request.Name.Trim(),
                    Email = request.Email.Trim().ToLower(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.Staff.Add(staff);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Created staff member {Name} ({Wallet}) by {CreatedBy}",
                    staff.Name, staff.WalletAddress, createdBy);

                var dto = new StaffDTO
                {
                    Id = staff.Id,
                    WalletAddress = staff.WalletAddress,
                    Name = staff.Name,
                    Email = staff.Email,
                    Avatar = staff.Avatar,
                    IsActive = staff.IsActive,
                    CreatedAt = staff.CreatedAt,
                    UpdatedAt = staff.UpdatedAt,
                    Roles = new List<RoleType>()
                };

                return (true, "Tạo nhân viên thành công", dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create staff member");
                return (false, "Lỗi hệ thống: " + ex.Message, null);
            }
        }

        public async Task<(bool Success, string Message)> GrantRoleAsync(
            int staffId, RoleType role, string grantedBy)
        {
            try
            {
                var staff = await _dbContext.Staff
                    .Include(s => s.StaffRoles)
                    .FirstOrDefaultAsync(s => s.Id == staffId && s.DeletedAt == null);

                if (staff == null)
                {
                    return (false, "Không tìm thấy nhân viên");
                }

                if (!staff.IsActive)
                {
                    return (false, "Nhân viên này đã bị vô hiệu hóa");
                }

                // Check if role already exists
                var existingRole = staff.StaffRoles.FirstOrDefault(sr => sr.Role == role);
                if (existingRole != null)
                {
                    return (false, $"Nhân viên đã có quyền {role}");
                }

                var staffRole = new StaffRole
                {
                    StaffId = staffId,
                    Role = role,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = grantedBy.ToLowerInvariant()
                };

                _dbContext.StaffRoles.Add(staffRole);
                staff.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Granted {Role} to staff {StaffId} ({Wallet}) by {GrantedBy}",
                    role, staffId, staff.WalletAddress, grantedBy);

                return (true, $"Đã cấp quyền {role} thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to grant role {Role} to staff {StaffId}", role, staffId);
                return (false, "Lỗi hệ thống: " + ex.Message);
            }
        }

        public async Task<(bool Success, string Message)> RevokeRoleAsync(
            int staffId, RoleType role)
        {
            try
            {
                var staff = await _dbContext.Staff
                    .Include(s => s.StaffRoles)
                    .FirstOrDefaultAsync(s => s.Id == staffId && s.DeletedAt == null);

                if (staff == null)
                {
                    return (false, "Không tìm thấy nhân viên");
                }

                var staffRole = staff.StaffRoles.FirstOrDefault(sr => sr.Role == role);
                if (staffRole == null)
                {
                    return (false, $"Nhân viên không có quyền {role}");
                }

                _dbContext.StaffRoles.Remove(staffRole);
                staff.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Revoked {Role} from staff {StaffId} ({Wallet})",
                    role, staffId, staff.WalletAddress);

                return (true, $"Đã thu hồi quyền {role} thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke role {Role} from staff {StaffId}", role, staffId);
                return (false, "Lỗi hệ thống: " + ex.Message);
            }
        }

        public async Task<(bool Success, string Message)> UpdateStaffStatusAsync(
            int staffId, bool isActive)
        {
            try
            {
                var staff = await _dbContext.Staff
                    .FirstOrDefaultAsync(s => s.Id == staffId && s.DeletedAt == null);

                if (staff == null)
                {
                    return (false, "Không tìm thấy nhân viên");
                }

                staff.IsActive = isActive;
                staff.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Updated staff {StaffId} status to {Status}",
                    staffId, isActive ? "Active" : "Inactive");

                return (true, $"Đã {(isActive ? "kích hoạt" : "vô hiệu hóa")} nhân viên");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update staff {StaffId} status", staffId);
                return (false, "Lỗi hệ thống: " + ex.Message);
            }
        }

        public async Task<(bool Success, string Message)> DeleteStaffAsync(int staffId)
        {
            try
            {
                var staff = await _dbContext.Staff
                    .FirstOrDefaultAsync(s => s.Id == staffId && s.DeletedAt == null);

                if (staff == null)
                {
                    return (false, "Không tìm thấy nhân viên");
                }

                staff.DeletedAt = DateTime.UtcNow;
                staff.IsActive = false;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Soft deleted staff {StaffId} ({Wallet})",
                    staffId, staff.WalletAddress);

                return (true, "Đã xóa nhân viên thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete staff {StaffId}", staffId);
                return (false, "Lỗi hệ thống: " + ex.Message);
            }
        }

        public async Task<List<RoleType>> GetStaffRolesForAuthAsync(string walletAddress)
        {
            var normalized = walletAddress.Trim().ToLowerInvariant();

            var staff = await _dbContext.Staff
                .Include(s => s.StaffRoles)
                .FirstOrDefaultAsync(s => 
                    s.WalletAddress.ToLower() == normalized && 
                    s.IsActive && 
                    s.DeletedAt == null);

            if (staff == null)
                return new List<RoleType>();

            return staff.StaffRoles.Select(sr => sr.Role).ToList();
        }
    }
}
