using InvestDapp.Shared.DTOs.Admin;
using InvestDapp.Shared.Models.Trading;

namespace InvestDapp.Application.TradingServices.Admin
{
    public interface IAdminTradingService
    {
        // Dashboard
        Task<TradingDashboardDto> GetDashboardAsync();

        // User Management
        Task<List<TopTraderDto>> GetAllTradersAsync(int page = 1, int pageSize = 50);
        Task<UserTradingDetailDto?> GetUserDetailAsync(string userWallet);
        
        // Withdrawal Management
        Task<List<PendingWithdrawalDto>> GetPendingWithdrawalsAsync();
        Task<bool> ApproveWithdrawalAsync(ApproveWithdrawalRequest request, string adminWallet);
        Task<bool> RejectWithdrawalAsync(RejectWithdrawalRequest request, string adminWallet);

        // Account Lock Management
        Task<bool> LockAccountAsync(LockAccountRequest request, string adminWallet);
        Task<bool> UnlockAccountAsync(UnlockAccountRequest request, string adminWallet);
        Task<List<TradingAccountLock>> GetActiveLocksAsync();

        // Balance Management
        Task<bool> AdjustUserBalanceAsync(AdjustBalanceRequest request, string adminWallet);

        // Fee Configuration
        Task<TradingFeeConfig?> GetActiveFeeConfigAsync();
        Task<bool> UpdateFeeConfigAsync(UpdateFeeConfigRequest request, string adminWallet);

        // Reports
        Task<byte[]> ExportTradingReportAsync(DateTime fromDate, DateTime toDate);
    }
}
