namespace InvestDapp.Shared.Enums
{
    /// <summary>
    /// Trạng thái yêu cầu rút tiền từ Trading Wallet
    /// </summary>
    public enum TradingWithdrawalStatus
    {
        Pending = 0,      // Đang chờ duyệt
        Approved = 1,     // Đã duyệt, chờ xử lý
        Completed = 2,    // Đã hoàn thành
        Rejected = 3,     // Bị từ chối
        Cancelled = 4     // User hủy
    }

    /// <summary>
    /// Loại giao dịch Balance
    /// </summary>
    public enum BalanceTransactionType
    {
        Deposit = 0,              // Nạp tiền
        Withdrawal = 1,           // Rút tiền
        TradingProfit = 2,        // Lợi nhuận giao dịch
        TradingLoss = 3,          // Lỗ giao dịch
        TradingFee = 4,           // Phí giao dịch
        WithdrawalFee = 5,        // Phí rút tiền
        AdminAdjustment = 6,      // Admin điều chỉnh
        Refund = 7                // Hoàn tiền
    }

    /// <summary>
    /// Loại khóa tài khoản
    /// </summary>
    public enum AccountLockType
    {
        None = 0,             // Không khóa
        TradingOnly = 1,      // Chỉ khóa giao dịch (có thể rút)
        WithdrawalOnly = 2,   // Chỉ khóa rút tiền (có thể giao dịch)
        Full = 3              // Khóa toàn bộ
    }

    /// <summary>
    /// Loại báo cáo phân tích
    /// </summary>
    public enum TradingReportType
    {
        Daily = 0,
        Weekly = 1,
        Monthly = 2,
        Custom = 3
    }
}
