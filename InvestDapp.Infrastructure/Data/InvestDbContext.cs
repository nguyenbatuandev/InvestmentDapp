using InvestDapp.Models;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Models.BlockchainModels;
using InvestDapp.Shared.Models.Kyc;
using InvestDapp.Shared.Models.Message;
using InvestDapp.Shared.Models.Support;
using InvestDapp.Shared.Models.Trading;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace InvestDapp.Infrastructure.Data
{
    public class InvestDbContext : DbContext
    {
        public InvestDbContext(DbContextOptions<InvestDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Trading model configurations
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.UserWallet).IsRequired().HasMaxLength(42);
                entity.Property(e => e.Price).HasPrecision(18, 8);
                entity.Property(e => e.StopPrice).HasPrecision(18, 8);
                entity.Property(e => e.Quantity).HasPrecision(18, 8);
                entity.Property(e => e.FilledQuantity).HasPrecision(18, 8);
                entity.Property(e => e.AvgPrice).HasPrecision(18, 8);
                entity.Property(e => e.TakeProfitPrice).HasPrecision(18,8);
                entity.Property(e => e.StopLossPrice).HasPrecision(18,8);
                entity.HasIndex(e => e.UserWallet);
                entity.HasIndex(e => e.Symbol);
                entity.HasIndex(e => e.Status);
            });

            modelBuilder.Entity<Position>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.UserWallet).IsRequired().HasMaxLength(42);
                entity.Property(e => e.Size).HasPrecision(18, 8);
                entity.Property(e => e.EntryPrice).HasPrecision(18, 8);
                entity.Property(e => e.MarkPrice).HasPrecision(18, 8);
                entity.Property(e => e.PnL).HasPrecision(18, 8);
                entity.Property(e => e.Margin).HasPrecision(18, 8);
                entity.Property(e => e.UnrealizedPnl).HasPrecision(18, 8);
                entity.Property(e => e.RealizedPnl).HasPrecision(18, 8);
                entity.Property(e => e.TakeProfitPrice).HasPrecision(18,8);
                entity.Property(e => e.StopLossPrice).HasPrecision(18,8);
                entity.Property(e => e.MaintenanceMarginRate).HasPrecision(9,6);
                entity.Property(e => e.LiquidationPrice).HasPrecision(18,8);
                entity.HasIndex(e => e.UserWallet);
                entity.HasIndex(e => e.Symbol);
            });

            modelBuilder.Entity<UserBalance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserWallet).IsRequired().HasMaxLength(42);
                entity.Property(e => e.Balance).HasPrecision(18, 8);
                entity.Property(e => e.AvailableBalance).HasPrecision(18, 8);
                entity.Property(e => e.MarginUsed).HasPrecision(18, 8);
                entity.Property(e => e.UnrealizedPnl).HasPrecision(18, 8);
                entity.HasIndex(e => e.UserWallet).IsUnique();
            });

            modelBuilder.Entity<BalanceTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserWallet).IsRequired().HasMaxLength(42);
                entity.Property(e => e.Amount).HasPrecision(18, 8);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
                entity.Property(e => e.BalanceAfter).HasPrecision(18, 8);
                entity.HasIndex(e => e.UserWallet);
                entity.HasIndex(e => e.Type);
            });

            modelBuilder.Entity<WalletWithdrawalRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserWallet).IsRequired().HasMaxLength(42);
                entity.Property(e => e.RecipientAddress).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Amount).HasPrecision(18, 8);
                entity.Property(e => e.Status).HasConversion<int>();
                entity.HasIndex(e => e.UserWallet);
                entity.HasIndex(e => e.Status);
            });

            modelBuilder.Entity<TradingFeeConfig>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.IsActive);
            });

            modelBuilder.Entity<TradingAccountLock>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserWallet).IsRequired().HasMaxLength(42);
                entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
                entity.Property(e => e.LockedByAdmin).IsRequired().HasMaxLength(42);
                entity.Property(e => e.LockType).HasConversion<int>();
                entity.HasIndex(e => e.UserWallet);
                entity.HasIndex(e => e.IsUnlocked);
            });

            modelBuilder.Entity<ProfitClaim>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ClaimerWallet).IsRequired().HasMaxLength(42);
                entity.HasIndex(e => e.ClaimerWallet);
                entity.HasIndex(e => e.ProfitId);
                entity.Property(e => e.TransactionHash).HasMaxLength(100);
            });
        }

        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<Category> Category { get; set; }
        public DbSet<Investment> Investment { get; set; }
        public DbSet<Vote> Vote { get; set; }
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }
        public DbSet<WalletWithdrawalRequest> WalletWithdrawalRequests { get; set; }

        public DbSet<EventLogBlockchain> EventLogBlockchain { get; set; }

        public DbSet<EventProcessingState> EventProcessingStates { get; set; }
        public DbSet<Profit> Profits { get; set; }
        public DbSet<ProfitClaim> ProfitClaims { get; set; }
        public DbSet<Refund> Refunds { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // KYC Models
        public DbSet<FundraiserKyc> FundraiserKyc { get; set; }
        public DbSet<IndividualKycInfo> IndividualKycInfos { get; set; }
        public DbSet<CompanyKycInfo> CompanyKycInfos { get; set; }

        // Message Models
        public DbSet<Messager> Messagers { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Participant> Participants { get; set; }
        public DbSet<MessageReadStatus> MessageReadStatuses { get; set; }
        public DbSet<CampaignPost> CampaignPosts { get; set; }

        // Trading Models
        public DbSet<Order> Orders { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<UserBalance> UserBalances { get; set; }
        public DbSet<BalanceTransaction> BalanceTransactions { get; set; }
        public DbSet<TradingFeeConfig> TradingFeeConfigs { get; set; }
        public DbSet<TradingAccountLock> TradingAccountLocks { get; set; }

        // Support/Ticketing Models
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<SupportTicketMessage> SupportTicketMessages { get; set; }
        public DbSet<SupportTicketAttachment> SupportTicketAttachments { get; set; }
        public DbSet<SupportTicketAssignment> SupportTicketAssignments { get; set; }

        // Staff Management Models
        public DbSet<Staff> Staff { get; set; }
        public DbSet<StaffRole> StaffRoles { get; set; }
    }
}
