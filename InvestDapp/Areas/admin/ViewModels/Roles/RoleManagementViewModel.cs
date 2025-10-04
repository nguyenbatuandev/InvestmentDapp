using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using InvestDapp.Shared.Enums;

namespace InvestDapp.Areas.Admin.ViewModels.Roles;

public class RoleManagementViewModel
{
    public IReadOnlyList<RoleOptionViewModel> AvailableRoles { get; init; } = new List<RoleOptionViewModel>();
    public IReadOnlyList<StaffMemberViewModel> StaffMembers { get; init; } = new List<StaffMemberViewModel>();
    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }
    public string GrantWalletInput { get; init; } = string.Empty;
    public string RevokeWalletInput { get; init; } = string.Empty;
    public RoleType? SelectedGrantRole { get; init; }
    public RoleType? SelectedRevokeRole { get; init; }
    public BlockchainStatusViewModel BlockchainStatus { get; init; } = BlockchainStatusViewModel.Empty;
}

public class RoleOptionViewModel
{
    public RoleType Role { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool RequiresSignature { get; init; }
    public string Category { get; init; } = "Off-chain";
    public string Value => Role.ToString();
    public string ModeLabel => RequiresSignature ? "On-chain" : "Off-chain";
    public string ModeDescription => RequiresSignature
        ? "Cần MetaMask ký giao dịch và được lưu trên smart contract."
        : "Chỉ lưu trong cơ sở dữ liệu nội bộ, không tạo giao dịch blockchain.";
}

public class BlockchainStatusViewModel
{
    public static readonly BlockchainStatusViewModel Empty = new();

    public bool IsConfigured { get; init; }
    public bool HasPrivateKey { get; init; }
    public bool HasAdminAddress { get; init; }
    public bool HasContractAddress { get; init; }
    public string? NormalizedAdminAddress { get; init; }
    public string? ContractAddress { get; init; }
    public long ChainId { get; init; }
    public string ChainIdHex { get; init; } = string.Empty;
    public string? RpcUrl { get; init; }
    public string[] Issues { get; init; } = Array.Empty<string>();
    public string[] Warnings { get; init; } = Array.Empty<string>();
    public bool ReadyForTransactions => IsConfigured && HasPrivateKey && HasContractAddress;
    public bool HasRpcEndpoint => !string.IsNullOrWhiteSpace(RpcUrl);
}

public class StaffMemberViewModel
{
    public string WalletAddress { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Email { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class RoleMutationRequest
{
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ ví.")]
    public string WalletAddress { get; set; } = string.Empty;

    [Required]
    public RoleType Role { get; set; }
}

public class ClientRoleMutationRequest
{
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ ví.")]
    public string WalletAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng cung cấp vai trò cần thao tác.")]
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Hành động không hợp lệ.")]
    public string Action { get; set; } = string.Empty;

    public string? TransactionHash { get; set; }
}
