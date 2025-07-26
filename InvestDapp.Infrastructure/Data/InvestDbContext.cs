using InvestDapp.Models;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Models.BlockchainModels;
using InvestDapp.Shared.Models.Kyc;
using InvestDapp.Shared.Models.Message;
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
        }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<Category> Category { get; set; }
        public DbSet<Investment> Investment { get; set; }
        public DbSet<Vote> Vote { get; set; }
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }

        public DbSet<EventLogBlockchain> EventLogBlockchain { get; set; }

        public DbSet<EventProcessingState> EventProcessingStates { get; set; }
        public DbSet<Profit> Profits { get; set; }
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

    }
}
