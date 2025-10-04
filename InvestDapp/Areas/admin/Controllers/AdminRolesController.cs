using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InvestDapp.Areas.Admin.ViewModels.Roles;
using InvestDapp.Application.AuthService.Admin;
using InvestDapp.Application.AuthService.Roles;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Shared.Security;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Util;
using Nethereum.Web3.Accounts;

namespace InvestDapp.Areas.admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AuthorizationPolicies.RequireSuperAdmin)]
    public class AdminRolesController : Controller
    {
        private readonly IRoleManagementService _roleManagementService;
        private readonly IRoleService _roleService;
        private readonly InvestDbContext _dbContext;
        private readonly ILogger<AdminRolesController> _logger;
        private readonly BlockchainConfig _blockchainConfig;

        private static readonly RoleOptionViewModel[] _roleOptions = new[]
        {
                new RoleOptionViewModel
                {
                    Role = RoleType.SuperAdmin,
                    Label = "Super Admin",
                    Description = "Quyền cao nhất, quản lý hệ thống và phân quyền on-chain.",
                    RequiresSignature = true,
                    Category = "On-chain"
                },
                new RoleOptionViewModel
                {
                    Role = RoleType.Admin,
                    Label = "Admin",
                    Description = "Toàn quyền quản trị chiến dịch, người dùng và cấu hình hệ thống.",
                    RequiresSignature = true,
                    Category = "On-chain"
                },
                new RoleOptionViewModel
                {
                    Role = RoleType.Fundraiser,
                    Label = "Fundraiser",
                    Description = "Cho phép tạo, chỉnh sửa và khởi chạy chiến dịch gọi vốn.",
                    RequiresSignature = false,
                    Category = "Off-chain"
                },
                new RoleOptionViewModel
                {
                    Role = RoleType.Moderator,
                    Label = "Moderator",
                    Description = "Quản lý nội dung, duyệt bình luận và giám sát vi phạm.",
                    RequiresSignature = false,
                    Category = "Off-chain"
                },
                new RoleOptionViewModel
                {
                    Role = RoleType.SupportAgent,
                    Label = "Support Agent",
                    Description = "Xử lý ticket hỗ trợ và giao tiếp với nhà đầu tư.",
                    RequiresSignature = false,
                    Category = "Off-chain"
                },
                new RoleOptionViewModel
                {
                    Role = RoleType.User,
                    Label = "User",
                    Description = "Quyền truy cập cơ bản cho người dùng thông thường.",
                    RequiresSignature = false,
                    Category = "Off-chain"
                }
        };

        public AdminRolesController(
            IRoleManagementService roleManagementService,
            IRoleService roleService,
            InvestDbContext dbContext,
            IOptions<BlockchainConfig> blockchainOptions,
            ILogger<AdminRolesController> logger)
        {
            _roleManagementService = roleManagementService;
            _roleService = roleService;
            _dbContext = dbContext;
            _blockchainConfig = blockchainOptions.Value;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var staffMembers = await GetStaffMembersAsync().ConfigureAwait(false);
            var blockchainStatus = BuildBlockchainStatus();
            var viewModel = BuildViewModel(staffMembers, blockchainStatus);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantRole([FromForm] RoleMutationRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                _logger.LogError("GrantRole: request is NULL");
                TempData["ErrorMessage"] = "Yêu cầu không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }
            
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("GrantRole ModelState invalid: {Errors}", errors);
                TempData["ErrorMessage"] = errors;
                PersistGrantState(request);
                return RedirectToAction(nameof(Index));
            }

            if (!TryNormalizeWallet(request.WalletAddress, out var normalizedWallet, out var normalizeError))
            {
                TempData["ErrorMessage"] = normalizeError ?? "Địa chỉ ví không hợp lệ.";
                PersistGrantState(request, normalizedWallet);
                return RedirectToAction(nameof(Index));
            }

            if (!RoleRequiresSignature(request.Role))
            {
                _logger.LogInformation("Off-chain grant: Starting for wallet {Wallet}, role {Role}", normalizedWallet, request.Role);
                
                try
                {
                    _roleService.InvalidateRoleCache(normalizedWallet);
                    
                    var offchainSyncWarning = await TrySyncStaffRoleAsync(normalizedWallet, request.Role, cancellationToken).ConfigureAwait(false);

                    var offchainMessage = $"Đã gán quyền {GetRoleLabel(request.Role)} (off-chain) cho {normalizedWallet}.";
                    if (!string.IsNullOrWhiteSpace(offchainSyncWarning))
                    {
                        offchainMessage += $" Lưu ý: {offchainSyncWarning}";
                    }

                    TempData["SuccessMessage"] = offchainMessage;
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Off-chain grant failed for wallet {Wallet}, role {Role}", normalizedWallet, request.Role);
                    TempData["ErrorMessage"] = $"Lỗi khi lưu quyền off-chain: {ex.Message}";
                    PersistGrantState(request, normalizedWallet);
                    return RedirectToAction(nameof(Index));
                }
            }

            var status = BuildBlockchainStatus();
            if (!status.ReadyForTransactions)
            {
                TempData["ErrorMessage"] = "Không thể cấp quyền on-chain vì cấu hình blockchain chưa sẵn sàng. Vui lòng kiểm tra thẻ 'Trạng thái blockchain'.";
                PersistGrantState(request, normalizedWallet);
                return RedirectToAction(nameof(Index));
            }

            var result = await _roleManagementService.GrantRoleAsync(request.Role, normalizedWallet, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Error ?? "Không thể cấp quyền.";
                PersistGrantState(request, result.NormalizedAddress ?? normalizedWallet);
                return RedirectToAction(nameof(Index));
            }

            var normalizedAddress = result.NormalizedAddress ?? normalizedWallet;
            _roleService.InvalidateRoleCache(normalizedAddress);

            var verification = await EnsureRoleStateAsync(
                    normalizedAddress,
                    request.Role,
                    shouldHaveRole: true,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!verification.Success)
            {
                TempData["ErrorMessage"] = verification.Error;
                PersistGrantState(request, normalizedAddress);
                return RedirectToAction(nameof(Index));
            }

            var syncWarning = await TrySyncStaffRoleAsync(normalizedAddress, request.Role, cancellationToken).ConfigureAwait(false);

            var successMessage = $"Đã cấp quyền {GetRoleLabel(request.Role)} cho {normalizedAddress}. Tx: {result.TransactionHash}";
            if (!string.IsNullOrWhiteSpace(syncWarning))
            {
                successMessage += $". Lưu ý: {syncWarning}";
            }

            TempData["SuccessMessage"] = successMessage;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeRole([FromForm] RoleMutationRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                _logger.LogError("RevokeRole: request is NULL");
                TempData["ErrorMessage"] = "Yêu cầu không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }
            
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                PersistRevokeState(request);
                return RedirectToAction(nameof(Index));
            }

            if (!TryNormalizeWallet(request.WalletAddress, out var normalizedWallet, out var normalizeError))
            {
                TempData["ErrorMessage"] = normalizeError ?? "Địa chỉ ví không hợp lệ.";
                PersistRevokeState(request, normalizedWallet);
                return RedirectToAction(nameof(Index));
            }

            if (!RoleRequiresSignature(request.Role))
            {
                _logger.LogInformation("Off-chain revoke: Starting for wallet {Wallet}, role {Role}", normalizedWallet, request.Role);
                
                try
                {
                    _roleService.InvalidateRoleCache(normalizedWallet);
                    _logger.LogInformation("==> Cache invalidated for {Wallet}", normalizedWallet);
                    
                    var offchainSyncWarning = await TrySyncStaffRoleAsync(normalizedWallet, null, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("==> TrySyncStaffRoleAsync completed. Warning: {Warning}", offchainSyncWarning ?? "(none)");

                    var offchainMessage = $"Đã thu hồi quyền {GetRoleLabel(request.Role)} (off-chain) của {normalizedWallet}.";
                    if (!string.IsNullOrWhiteSpace(offchainSyncWarning))
                    {
                        offchainMessage += $" Lưu ý: {offchainSyncWarning}";
                    }

                    TempData["SuccessMessage"] = offchainMessage;
                    _logger.LogInformation("==> OFF-CHAIN REVOKE SUCCESS: {Message}", offchainMessage);
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "==> OFF-CHAIN REVOKE FAILED for wallet {Wallet}, role {Role}", normalizedWallet, request.Role);
                    TempData["ErrorMessage"] = $"Lỗi khi thu hồi quyền off-chain: {ex.Message}";
                    PersistRevokeState(request, normalizedWallet);
                    return RedirectToAction(nameof(Index));
                }
            }

            var status = BuildBlockchainStatus();
            if (!status.ReadyForTransactions)
            {
                TempData["ErrorMessage"] = "Không thể thu hồi quyền on-chain vì cấu hình blockchain chưa sẵn sàng. Vui lòng kiểm tra thẻ 'Trạng thái blockchain'.";
                PersistRevokeState(request, normalizedWallet);
                return RedirectToAction(nameof(Index));
            }

            var result = await _roleManagementService.RevokeRoleAsync(request.Role, normalizedWallet, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Error ?? "Không thể thu hồi quyền.";
                PersistRevokeState(request, result.NormalizedAddress ?? normalizedWallet);
                return RedirectToAction(nameof(Index));
            }

            var normalizedAddress = result.NormalizedAddress ?? normalizedWallet;
            _roleService.InvalidateRoleCache(normalizedAddress);

            var verification = await EnsureRoleStateAsync(
                    normalizedAddress,
                    request.Role,
                    shouldHaveRole: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!verification.Success)
            {
                TempData["ErrorMessage"] = verification.Error;
                PersistRevokeState(request, normalizedAddress);
                return RedirectToAction(nameof(Index));
            }

            var syncWarning = await TrySyncStaffRoleAsync(normalizedAddress, null, cancellationToken).ConfigureAwait(false);

            var successMessage = $"Đã thu hồi quyền {GetRoleLabel(request.Role)} của {normalizedAddress}. Tx: {result.TransactionHash}";
            if (!string.IsNullOrWhiteSpace(syncWarning))
            {
                successMessage += $". Lưu ý: {syncWarning}";
            }

            TempData["SuccessMessage"] = successMessage;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/admin/api/roles/sync")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncRoleFromClient([FromBody] ClientRoleMutationRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest(new { success = false, error = "Yêu cầu không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                var message = string.IsNullOrWhiteSpace(errors) ? "Yêu cầu không hợp lệ." : errors;
                return BadRequest(new { success = false, error = message });
            }

            var action = request.Action?.Trim().ToLowerInvariant();
            if (action != "grant" && action != "revoke")
            {
                return BadRequest(new { success = false, error = "Hành động không hợp lệ." });
            }

            if (!Enum.TryParse<RoleType>(request.Role, true, out var parsedRole))
            {
                return BadRequest(new { success = false, error = "Vai trò không hợp lệ." });
            }

            if (!RoleRequiresSignature(parsedRole))
            {
                return BadRequest(new { success = false, error = "Vai trò này được quản lý off-chain và không cần đồng bộ blockchain." });
            }

            if (!TryNormalizeWallet(request.WalletAddress, out var normalizedWallet, out var validationError))
            {
                return BadRequest(new { success = false, error = validationError ?? "Địa chỉ ví không hợp lệ." });
            }

            var shouldHaveRole = string.Equals(action, "grant", StringComparison.Ordinal);

            try
            {
                _roleService.InvalidateRoleCache(normalizedWallet);
                var hasRole = await _roleService.HasRoleAsync(normalizedWallet, parsedRole).ConfigureAwait(false);
                if (hasRole != shouldHaveRole)
                {
                    var label = GetRoleLabel(parsedRole);
                    var message = shouldHaveRole
                        ? $"Quyền {label} chưa được ghi nhận trên blockchain. Vui lòng đợi thêm và thử lại."
                        : $"Quyền {label} vẫn còn tồn tại trên blockchain. Vui lòng kiểm tra trạng thái giao dịch.";
                    return Conflict(new { success = false, error = message });
                }

                var syncWarning = await TrySyncStaffRoleAsync(normalizedWallet, shouldHaveRole ? parsedRole : null, cancellationToken).ConfigureAwait(false);

                return Ok(new
                {
                    success = true,
                    wallet = normalizedWallet,
                    role = parsedRole.ToString(),
                    action,
                    transactionHash = request.TransactionHash,
                    warning = syncWarning
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể đồng bộ kết quả role mutation cho ví {Wallet}", normalizedWallet);
                return StatusCode(500, new { success = false, error = "Không thể đồng bộ dữ liệu. Vui lòng thử lại sau." });
            }
        }

        private RoleManagementViewModel BuildViewModel(IReadOnlyList<StaffMemberViewModel> staffMembers, BlockchainStatusViewModel status)
        {
            return new RoleManagementViewModel
            {
                AvailableRoles = _roleOptions,
                StaffMembers = staffMembers,
                SuccessMessage = TempData["SuccessMessage"] as string,
                ErrorMessage = TempData["ErrorMessage"] as string,
                GrantWalletInput = TempData["GrantWalletInput"] as string ?? string.Empty,
                RevokeWalletInput = TempData["RevokeWalletInput"] as string ?? string.Empty,
                SelectedGrantRole = ParseRole(TempData["GrantRoleInput"] as string),
                SelectedRevokeRole = ParseRole(TempData["RevokeRoleInput"] as string),
                BlockchainStatus = status
            };
        }

        private void PersistGrantState(RoleMutationRequest request, string? normalizedAddress = null)
        {
            TempData["GrantWalletInput"] = normalizedAddress ?? request.WalletAddress;
            TempData["GrantRoleInput"] = request.Role.ToString();
        }

        private void PersistRevokeState(RoleMutationRequest request, string? normalizedAddress = null)
        {
            TempData["RevokeWalletInput"] = normalizedAddress ?? request.WalletAddress;
            TempData["RevokeRoleInput"] = request.Role.ToString();
        }

        private static RoleType? ParseRole(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Enum.TryParse<RoleType>(value, true, out var parsed) ? parsed : null;
        }

        private static string GetRoleLabel(RoleType role)
        {
            var option = _roleOptions.FirstOrDefault(r => r.Role == role);
            return option?.Label ?? role.ToString();
        }

        private static bool RoleRequiresSignature(RoleType role)
        {
            return _roleOptions.Any(option => option.Role == role && option.RequiresSignature);
        }

        private async Task<IReadOnlyList<StaffMemberViewModel>> GetStaffMembersAsync()
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(u => !string.IsNullOrEmpty(u.Role) && u.Role != null && u.Role.ToLower() != "user")
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .Select(u => new StaffMemberViewModel
                {
                    WalletAddress = u.WalletAddress,
                    Role = u.Role,
                    Name = u.Name,
                    Email = u.Email,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }

        private async Task<string?> TrySyncStaffRoleAsync(string walletAddress, RoleType? role, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
            {
                _logger.LogWarning("==> TrySyncStaffRoleAsync: Empty wallet address provided");
                return "Không xác định được ví để đồng bộ dữ liệu nhân sự.";
            }

            var normalized = walletAddress.Trim();
            var lowered = normalized.ToLowerInvariant();
            _logger.LogInformation("==> TrySyncStaffRoleAsync: wallet={Wallet}, role={Role}, lowered={Lowered}", normalized, role?.ToString() ?? "NULL", lowered);

            try
            {
                var targetRole = role.HasValue ? role.Value.ToString() : "user";
                _logger.LogInformation("==> Attempting ExecuteUpdateAsync: targetRole={TargetRole}", targetRole);
                
                var affectedRows = await _dbContext.Users
                    .Where(u => u.WalletAddress.ToLower() == lowered)
                    .ExecuteUpdateAsync(updates => updates
                        .SetProperty(u => u.Role, targetRole)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow), cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation("==> ExecuteUpdateAsync completed: affectedRows={AffectedRows}", affectedRows);

                if (affectedRows > 0)
                {
                    _logger.LogInformation("==> User role updated successfully in database");
                    return null;
                }

                if (!role.HasValue)
                {
                    _logger.LogWarning("==> No user found and role is null, cannot create new user");
                    return "Không tìm thấy tài khoản nội bộ khớp với ví này. Bảng nhân sự sẽ cập nhật sau khi tài khoản tồn tại.";
                }

                _logger.LogInformation("==> No existing user found, creating new user record");
                var now = DateTime.UtcNow;
                var newUser = new User
                {
                    WalletAddress = normalized,
                    Email = GeneratePlaceholderEmail(normalized),
                    Name = normalized,
                    Role = targetRole,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _logger.LogInformation("==> Adding new user to context: WalletAddress={Wallet}, Role={Role}", normalized, targetRole);
                await _dbContext.Users.AddAsync(newUser, cancellationToken).ConfigureAwait(false);
                
                _logger.LogInformation("==> Calling SaveChangesAsync");
                var savedCount = await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("==> SaveChangesAsync completed: savedCount={SavedCount}", savedCount);
                
                return "Đã tự động tạo hồ sơ nhân sự mới từ ví này. Vui lòng cập nhật thông tin liên hệ.";
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "==> DbUpdateConcurrencyException for wallet {Wallet}", normalized);
                return "Có xung đột khi cập nhật dữ liệu nhân sự. Vui lòng thử lại.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "==> DbUpdateException for wallet {Wallet}. InnerException: {Inner}", normalized, ex.InnerException?.Message);
                return "Không thể cập nhật dữ liệu nội bộ. Vui lòng kiểm tra lại sau.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "==> UNEXPECTED EXCEPTION in TrySyncStaffRoleAsync for wallet {Wallet}", normalized);
                throw;
            }
        }

        private async Task<(bool Success, string? Error)> EnsureRoleStateAsync(
            string walletAddress,
            RoleType role,
            bool shouldHaveRole,
            CancellationToken cancellationToken,
            int maxAttempts = 5,
            int delayMilliseconds = 750)
        {
            Exception? lastException = null;

            for (var attempt = 1; attempt <= Math.Max(1, maxAttempts); attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _roleService.InvalidateRoleCache(walletAddress);
                    var hasRole = await _roleService.HasRoleAsync(walletAddress, role).ConfigureAwait(false);
                    if (hasRole == shouldHaveRole)
                    {
                        return (true, null);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Lỗi khi kiểm tra quyền {Role} cho ví {Wallet} (lần {Attempt}/{MaxAttempts})", role, walletAddress, attempt, maxAttempts);
                }

                if (attempt < maxAttempts)
                {
                    try
                    {
                        await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
            }

            if (lastException is not null)
            {
                _logger.LogError(lastException, "Không thể xác minh quyền {Role} cho ví {Wallet} sau {Attempts} lần", role, walletAddress, maxAttempts);
                return (false, "Không thể xác minh trạng thái quyền trên blockchain. Vui lòng thử lại sau hoặc kiểm tra nhật ký hệ thống.");
            }

            var action = shouldHaveRole ? "Cấp quyền" : "Thu hồi quyền";
            var expectation = shouldHaveRole ? "chưa được ghi nhận trên blockchain" : "vẫn còn tồn tại trên blockchain";
            var label = GetRoleLabel(role);
            return (false, $"{action} {label} {expectation} sau {maxAttempts} lần kiểm tra. Vui lòng kiểm tra trạng thái giao dịch hoặc đợi thêm trước khi thử lại.");
        }

        private static string GeneratePlaceholderEmail(string checksumAddress)
        {
            var localPart = checksumAddress
                .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
                .ToLowerInvariant();

            return $"{localPart}@autogen.staff";
        }

        private BlockchainStatusViewModel BuildBlockchainStatus()
        {
            var issues = new List<string>();
            var warnings = new List<string>();
            var addressUtil = new AddressUtil();
            var chainId = _blockchainConfig.ChainId;
            var chainIdHex = chainId > 0 ? $"0x{chainId:X}" : string.Empty;
            var rpcUrl = string.IsNullOrWhiteSpace(_blockchainConfig.RpcUrl)
                ? null
                : _blockchainConfig.RpcUrl.Trim();

            var hasPrivateKey = !string.IsNullOrWhiteSpace(_blockchainConfig.DefaultAdminPrivateKey);
            var normalizedAdminAddress = default(string?);
            var hasAdminAddress = false;
            var hasContractAddress = false;
            var contractAddress = default(string?);

            if (hasPrivateKey)
            {
                try
                {
                    var account = new Account(_blockchainConfig.DefaultAdminPrivateKey, _blockchainConfig.ChainId);
                    normalizedAdminAddress = addressUtil.ConvertToChecksumAddress(account.Address);
                    hasAdminAddress = true;
                }
                catch (Exception ex)
                {
                    hasPrivateKey = false;
                    issues.Add("Private key cho ví quản trị mặc định không hợp lệ.");
                    _logger.LogError(ex, "Không thể khởi tạo tài khoản từ private key cấu hình");
                }
            }
            else if (!string.IsNullOrWhiteSpace(_blockchainConfig.DefaultAdminAddress))
            {
                try
                {
                    normalizedAdminAddress = addressUtil.ConvertToChecksumAddress(_blockchainConfig.DefaultAdminAddress);
                    hasAdminAddress = true;
                    warnings.Add("Đã cấu hình địa chỉ quản trị nhưng thiếu private key nên không thể ký giao dịch server-side.");
                }
                catch (Exception)
                {
                    issues.Add("Địa chỉ quản trị mặc định không hợp lệ.");
                }
            }
            else
            {
                issues.Add("Chưa cấu hình private key cho tài khoản quản trị mặc định.");
            }

            var candidate = string.IsNullOrWhiteSpace(_blockchainConfig.RoleManagerContractAddress) || IsZeroAddress(_blockchainConfig.RoleManagerContractAddress)
                ? _blockchainConfig.ContractAddress
                : _blockchainConfig.RoleManagerContractAddress;

            if (!string.IsNullOrWhiteSpace(candidate) && addressUtil.IsValidEthereumAddressHexFormat(candidate) && !IsZeroAddress(candidate))
            {
                contractAddress = addressUtil.ConvertToChecksumAddress(candidate);
                hasContractAddress = true;
            }
            else
            {
                issues.Add("Địa chỉ smart contract quản lý quyền chưa hợp lệ.");
            }

            var isConfigured = hasPrivateKey && hasContractAddress;

            return new BlockchainStatusViewModel
            {
                IsConfigured = isConfigured,
                HasPrivateKey = hasPrivateKey,
                HasAdminAddress = hasAdminAddress,
                HasContractAddress = hasContractAddress,
                NormalizedAdminAddress = normalizedAdminAddress,
                ContractAddress = contractAddress,
                ChainId = chainId,
                ChainIdHex = chainIdHex,
                RpcUrl = rpcUrl,
                Issues = issues.Distinct().ToArray(),
                Warnings = warnings.Distinct().ToArray()
            };
        }

        private static bool TryNormalizeWallet(string? walletAddress, out string normalized, out string? error)
        {
            normalized = string.Empty;
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

            normalized = addressUtil.ConvertToChecksumAddress(trimmed);
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
    }
}
