using System;
using System.Collections.Generic;
using InvestDapp.Shared.Enums;

namespace InvestDapp.Shared.DTOs
{
    public class StaffDTO
    {
        public int Id { get; set; }
        public string WalletAddress { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<RoleType> Roles { get; set; } = new();
    }

    public class CreateStaffRequest
    {
        public string WalletAddress { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class GrantStaffRoleRequest
    {
        public int StaffId { get; set; }
        public RoleType Role { get; set; }
    }

    public class RevokeStaffRoleRequest
    {
        public int StaffId { get; set; }
        public RoleType Role { get; set; }
    }

    public class UpdateStaffStatusRequest
    {
        public int StaffId { get; set; }
        public bool IsActive { get; set; }
    }
}
