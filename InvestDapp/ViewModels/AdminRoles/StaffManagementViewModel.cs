using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using InvestDapp.Shared.DTOs;
using InvestDapp.Shared.Enums;

namespace InvestDapp.ViewModels.AdminRoles
{
    public class StaffManagementViewModel
    {
        public List<StaffDTO> StaffList { get; set; } = new();
        public List<RoleOption> AvailableRoles { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class CreateStaffViewModel
    {
        [Required(ErrorMessage = "Địa chỉ ví là bắt buộc")]
        [RegularExpression(@"^0x[a-fA-F0-9]{40}$", ErrorMessage = "Địa chỉ ví không hợp lệ (phải là 0x + 40 ký tự hex)")]
        public string WalletAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên là bắt buộc")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "Tên phải từ 2-255 ký tự")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;
    }

    public class RoleOption
    {
        public RoleType Role { get; set; }
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool RequiresSignature { get; set; }
        public string ModeLabel { get; set; } = string.Empty;
        public string ModeDescription { get; set; } = string.Empty;
    }
}
