using InvestDapp.Application.AuthService.Admin;
using InvestDapp.Application.AuthService.Roles;
using InvestDapp.Application.AuthService.Staff;
using InvestDapp.Shared.DTOs;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Security;
using InvestDapp.ViewModels.AdminRoles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nethereum.Util;

namespace InvestDapp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AuthorizationPolicies.RequireSuperAdmin)]
    public class StaffManagementController : Controller
    {
        private readonly IStaffManagementService _staffService;
        private readonly IRoleManagementService _roleManagementService;
        private readonly IRoleService _roleService;
        private readonly ILogger<StaffManagementController> _logger;

        private static readonly RoleOption[] _availableRoles = new[]
        {
            new RoleOption
            {
                Role = RoleType.SuperAdmin,
                Value = "SuperAdmin",
                Label = "Super Admin",
                Description = "Toàn quyền hệ thống, quản lý tất cả",
                Category = "Leadership",
                RequiresSignature = true,
                ModeLabel = "On-chain",
                ModeDescription = "Ký MetaMask & ghi smart contract"
            },
            new RoleOption
            {
                Role = RoleType.Admin,
                Value = "Admin",
                Label = "Admin",
                Description = "Quản trị viên, phê duyệt KYC/Campaign",
                Category = "Leadership",
                RequiresSignature = true,
                ModeLabel = "On-chain",
                ModeDescription = "Ký MetaMask & ghi smart contract"
            },
            new RoleOption
            {
                Role = RoleType.Moderator,
                Value = "Moderator",
                Label = "Moderator",
                Description = "Kiểm duyệt nội dung, xem báo cáo",
                Category = "Operations",
                RequiresSignature = false,
                ModeLabel = "Off-chain",
                ModeDescription = "Lưu trữ trong database"
            },
            new RoleOption
            {
                Role = RoleType.SupportAgent,
                Value = "SupportAgent",
                Label = "Support Agent",
                Description = "Hỗ trợ khách hàng, quản lý tickets",
                Category = "Support",
                RequiresSignature = false,
                ModeLabel = "Off-chain",
                ModeDescription = "Lưu trữ trong database"
            },
            new RoleOption
            {
                Role = RoleType.Fundraiser,
                Value = "Fundraiser",
                Label = "Fundraiser",
                Description = "Quản lý chiến dịch gây quỹ",
                Category = "Operations",
                RequiresSignature = false,
                ModeLabel = "Off-chain",
                ModeDescription = "Lưu trữ trong database"
            }
        };

        public StaffManagementController(
            IStaffManagementService staffService,
            IRoleManagementService roleManagementService,
            IRoleService roleService,
            ILogger<StaffManagementController> logger)
        {
            _staffService = staffService;
            _roleManagementService = roleManagementService;
            _roleService = roleService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? message, bool? success)
        {
            var staffList = await _staffService.GetAllStaffAsync();

            var viewModel = new StaffManagementViewModel
            {
                StaffList = staffList,
                AvailableRoles = _availableRoles.ToList(),
                SuccessMessage = success == true ? message : null,
                ErrorMessage = success == false ? message : null
            };

            return View(viewModel);
        }

        [HttpPost]
        [AllowAnonymous] // DEBUG: Remove all auth
        //[ValidateAntiForgeryToken] // DEBUG: Disable antiforgery
        public async Task<IActionResult> CreateStaff(string walletAddress, string name, string email)
        {
            _logger.LogCritical("==> CreateStaff CALLED! walletAddress={Wallet}, name={Name}, email={Email}", 
                walletAddress ?? "NULL", name ?? "NULL", email ?? "NULL");
            
            try
            {
                // Manual validation since we're using individual parameters
                if (string.IsNullOrWhiteSpace(walletAddress) || 
                    string.IsNullOrWhiteSpace(name) || 
                    string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogCritical("==> Validation FAILED: Empty fields");
                    return RedirectToAction(nameof(Index), new
                    {
                        message = "Vui lòng điền đầy đủ thông tin",
                        success = false
                    });
                }

                // Validate wallet format
                if (!System.Text.RegularExpressions.Regex.IsMatch(walletAddress, @"^0x[a-fA-F0-9]{40}$"))
                {
                    _logger.LogCritical("==> Wallet format INVALID: {Wallet}", walletAddress);
                    return RedirectToAction(nameof(Index), new
                    {
                        message = "Địa chỉ ví không hợp lệ (phải là 0x + 40 ký tự hex)",
                        success = false
                    });
                }

                var currentWallet = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
                _logger.LogCritical("==> Current user wallet: {Wallet}", currentWallet);

                var request = new CreateStaffRequest
                {
                    WalletAddress = walletAddress,
                    Name = name,
                    Email = email
                };

                _logger.LogCritical("==> Calling service...");
                var (isSuccess, message, staff) = await _staffService.CreateStaffAsync(request, currentWallet);

                _logger.LogCritical("==> Service returned: success={Success}, message={Message}, staffId={StaffId}", 
                    isSuccess, message, staff?.Id ?? 0);

                if (isSuccess)
                {
                    _logger.LogInformation("Staff created: {Name} ({Wallet}) by {Creator}",
                        name, walletAddress, currentWallet);

                    return RedirectToAction(nameof(Index), new
                    {
                        message = message + $" - ID: {staff?.Id}",
                        success = true
                    });
                }

                return RedirectToAction(nameof(Index), new
                {
                    message = message,
                    success = false
                });
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "==> EXCEPTION in CreateStaff: {Message}", ex.Message);
                return RedirectToAction(nameof(Index), new
                {
                    message = "Lỗi hệ thống: " + ex.Message,
                    success = false
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GrantRole(
            int staffId, 
            RoleType role, 
            bool alreadySigned = false, 
            string? transactionHash = null)
        {
            var currentWallet = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";

            // Get staff to retrieve wallet address
            var staff = await _staffService.GetStaffByIdAsync(staffId);
            if (staff == null)
            {
                return RedirectToAction(nameof(Index), new
                {
                    message = "Không tìm thấy nhân viên",
                    success = false
                });
            }

            // Check if role requires on-chain signature
            var roleOption = _availableRoles.FirstOrDefault(r => r.Role == role);
            bool requiresSignature = roleOption?.RequiresSignature ?? false;

            string message;
            bool isSuccess;

            if (requiresSignature)
            {
                if (alreadySigned)
                {
                    // CLIENT ALREADY SIGNED - Just update database
                    _logger.LogInformation("Client-side signature completed for {Role}, updating database only. Tx: {TxHash}", 
                        role, transactionHash ?? "unknown");
                    
                    var normalizedWallet = new AddressUtil().ConvertToChecksumAddress(staff.WalletAddress);
                    _roleService.InvalidateRoleCache(normalizedWallet);
                    
                    var (dbSuccess, dbMessage) = await _staffService.GrantRoleAsync(staffId, role, currentWallet);
                    
                    message = dbSuccess 
                        ? $"✅ Đã cấp quyền {roleOption?.Label} (on-chain) cho {staff.Name}. Tx: {transactionHash}"
                        : $"⚠️ Blockchain thành công nhưng database lỗi: {dbMessage}";
                    isSuccess = dbSuccess;
                }
                else
                {
                    // SERVER-SIDE SIGNING (fallback)
                    _logger.LogInformation("Server-side on-chain grant: Role {Role} for wallet {Wallet}", role, staff.WalletAddress);
                    
                    var normalizedWallet = new AddressUtil().ConvertToChecksumAddress(staff.WalletAddress);
                    var blockchainResult = await _roleManagementService.GrantRoleAsync(role, normalizedWallet, CancellationToken.None);
                    
                    if (!blockchainResult.Success)
                    {
                        message = blockchainResult.Error ?? "Không thể cấp quyền on-chain";
                        isSuccess = false;
                    }
                    else
                    {
                        // Invalidate cache and grant in database
                        _roleService.InvalidateRoleCache(normalizedWallet);
                        var (dbSuccess, dbMessage) = await _staffService.GrantRoleAsync(staffId, role, currentWallet);
                        
                        message = dbSuccess 
                            ? $"Đã cấp quyền {roleOption?.Label} (on-chain) cho {staff.Name}. Tx: {blockchainResult.TransactionHash}"
                            : $"Blockchain thành công nhưng database lỗi: {dbMessage}";
                        isSuccess = dbSuccess;
                    }
                }
            }
            else
            {
                // OFF-CHAIN: Only database
                _logger.LogInformation("Off-chain grant: Role {Role} for staff {StaffId}", role, staffId);
                (isSuccess, message) = await _staffService.GrantRoleAsync(staffId, role, currentWallet);
                
                if (isSuccess)
                {
                    message = $"Đã cấp quyền {roleOption?.Label} (off-chain) cho {staff.Name}";
                }
            }

            if (isSuccess)
            {
                _logger.LogInformation("Role {Role} granted to staff {StaffId} by {Granter}",
                    role, staffId, currentWallet);
            }
            else
            {
                _logger.LogWarning("Failed to grant role {Role} to staff {StaffId}: {Message}",
                    role, staffId, message);
            }

            return RedirectToAction(nameof(Index), new
            {
                message = message,
                success = isSuccess
            });
        }

        [HttpPost]
        public async Task<IActionResult> RevokeRole(
            int staffId, 
            RoleType role,
            bool alreadySigned = false,
            string? transactionHash = null)
        {
            // Get staff to check if role is on-chain
            var staff = await _staffService.GetStaffByIdAsync(staffId);
            if (staff == null)
            {
                return RedirectToAction(nameof(Index), new
                {
                    message = "Không tìm thấy nhân viên",
                    success = false
                });
            }

            var roleOption = _availableRoles.FirstOrDefault(r => r.Role == role);
            bool requiresSignature = roleOption?.RequiresSignature ?? false;

            string message;
            bool isSuccess;

            if (requiresSignature && alreadySigned)
            {
                // CLIENT ALREADY SIGNED - Just update database
                _logger.LogInformation("Client-side revoke completed for {Role}, updating database only. Tx: {TxHash}", 
                    role, transactionHash ?? "unknown");
                
                var normalizedWallet = new AddressUtil().ConvertToChecksumAddress(staff.WalletAddress);
                _roleService.InvalidateRoleCache(normalizedWallet);
                
                (isSuccess, message) = await _staffService.RevokeRoleAsync(staffId, role);
                
                if (isSuccess)
                {
                    message = $"✅ Đã thu hồi quyền {roleOption?.Label} (on-chain) từ {staff.Name}. Tx: {transactionHash}";
                }
            }
            else
            {
                // OFF-CHAIN or SERVER-SIDE
                (isSuccess, message) = await _staffService.RevokeRoleAsync(staffId, role);
                
                if (isSuccess && roleOption != null)
                {
                    message = $"Đã thu hồi quyền {roleOption.Label} ({roleOption.ModeLabel}) từ {staff.Name}";
                }
            }

            if (isSuccess)
            {
                _logger.LogInformation("Role {Role} revoked from staff {StaffId}", role, staffId);
            }
            else
            {
                _logger.LogWarning("Failed to revoke role {Role} from staff {StaffId}: {Message}",
                    role, staffId, message);
            }

            return RedirectToAction(nameof(Index), new
            {
                message = message,
                success = isSuccess
            });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int staffId)
        {
            var staff = await _staffService.GetStaffByIdAsync(staffId);
            if (staff == null)
            {
                return RedirectToAction(nameof(Index), new
                {
                    message = "Không tìm thấy nhân viên",
                    success = false
                });
            }

            var (isSuccess, message) = await _staffService.UpdateStaffStatusAsync(staffId, !staff.IsActive);

            return RedirectToAction(nameof(Index), new
            {
                message = message,
                success = isSuccess
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int staffId)
        {
            var (isSuccess, message) = await _staffService.DeleteStaffAsync(staffId);

            if (isSuccess)
            {
                _logger.LogInformation("Staff {StaffId} deleted", staffId);
            }

            return RedirectToAction(nameof(Index), new
            {
                message = message,
                success = isSuccess
            });
        }
    }
}
