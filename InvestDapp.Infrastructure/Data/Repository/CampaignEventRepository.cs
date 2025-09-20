using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Models;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Models.BlockchainModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Nethereum.Web3;
using System.Numerics;
namespace InvestDapp.Infrastructure.Data.Repository
{
    public class CampaignEventRepository : ICampaignEventRepository
    {
        private readonly InvestDbContext _investDbContext;

        public CampaignEventRepository(InvestDbContext investDbContext)
        {
            _investDbContext = investDbContext;
        }
        public async Task HandleCampaignCreatedAsync(BigInteger campaignId, string owner, string name, BigInteger goalAmount, BigInteger endTime, DateTime time)
        {
            var existing = await _investDbContext.Campaigns.FindAsync((int)campaignId);
            if (existing != null) return;

            var campaign = new Campaign
            {
                Id = (int)campaignId,
                OwnerAddress = owner,
                Name = name,
                GoalAmount = (double)Web3.Convert.FromWei(goalAmount),
                EndTime = DateTimeOffset.FromUnixTimeSeconds((long)endTime).UtcDateTime,
                Status = CampaignStatus.Active,
                CurrentRaisedAmount = 0,
                TotalInvestmentsOnCompletion = 0,
                TotalProfitAdded = 0,
                InvestorCount = 0,
                DeniedWithdrawalRequestCount = 0,
                CreatedAt = time
            };

            _investDbContext.Campaigns.Add(campaign);
            await _investDbContext.SaveChangesAsync();
        }


        public async Task HandleInvestmentReceivedAsync(BigInteger campaignId, string investor, BigInteger amount, BigInteger currentRaisedAmount, string transactionHash, DateTime time)
        {
            // Tìm chiến dịch, nếu không có thì bỏ qua
            var campaign = await _investDbContext.Campaigns.FindAsync((int)campaignId);
            if (campaign == null) return;

            // 1. Kiểm tra trùng lặp bằng Transaction Hash
            // Đây là cách kiểm tra đáng tin cậy nhất
            var isTransactionProcessed = await _investDbContext.Investment
                .AnyAsync(i => i.TransactionHash == transactionHash);

            if (isTransactionProcessed)
            {
                // Nếu transaction đã được xử lý, chỉ cần đảm bảo CurrentRaisedAmount được cập nhật và thoát
                // Điều này giúp hệ thống tự sửa lỗi nếu lần xử lý trước bị trục trặc
                campaign.CurrentRaisedAmount = (double)Web3.Convert.FromWei(currentRaisedAmount);
                await _investDbContext.SaveChangesAsync();
                return;
            }

            // --- Nếu là transaction mới, tiếp tục xử lý ---

            // 2. Cập nhật thông tin cho Campaign
            campaign.CurrentRaisedAmount = (double)Web3.Convert.FromWei(currentRaisedAmount);

            // Kiểm tra xem đây có phải là nhà đầu tư mới cho chiến dịch này không
            var isNewInvestor = !await _investDbContext.Investment
                .AnyAsync(i => i.CampaignId == (int)campaignId && i.InvestorAddress == investor);

            if (isNewInvestor)
            {
                campaign.InvestorCount++;
            }

            // 3. Tạo bản ghi Investment mới
            var newInvestment = new Investment
            {
                TransactionHash = transactionHash,
                CampaignId = (int)campaignId,
                InvestorAddress = investor,
                Amount = (double)Web3.Convert.FromWei(amount),
                Timestamp = time,
            };

            // 4. Thêm vào DbContext
            _investDbContext.Investment.Add(newInvestment);

            // 5. Lưu tất cả thay đổi (cả Campaign và Investment mới) vào DB
            // EF Core sẽ thực hiện việc này trong một transaction duy nhất, đảm bảo an toàn.
            await _investDbContext.SaveChangesAsync();
        }

        public async Task HandleProfitAddedAsync(BigInteger id ,BigInteger campaignId, BigInteger amount, string txhash, DateTime time)
        {
            var campaign = await _investDbContext.Campaigns.FindAsync((int)campaignId);
            if (campaign == null) return;

            var newProfit = new Profit
            {
                Id = (int)id,
                CampaignId = (int)campaignId,
                Amount = (double)Web3.Convert.FromWei(amount),
                TransactionHash = txhash,
                CreatedAt = time
            };
            campaign.TotalProfitAdded += (double)amount;
            await _investDbContext.SaveChangesAsync();
        }


        public async Task HandleCampaignStatusUpdatedAsync(BigInteger campaignId, byte newStatus)
        {
            var campaign = await _investDbContext.Campaigns.FindAsync((int)campaignId);
            var b = newStatus;

            var a = Enum.IsDefined(typeof(CampaignStatus), (int)newStatus);

            if (campaign == null) return;

                var status = (CampaignStatus)newStatus;
                campaign.Status = status;

                if (status == CampaignStatus.Completed)
                {
                    campaign.TotalInvestmentsOnCompletion = campaign.CurrentRaisedAmount;
                }

                await _investDbContext.SaveChangesAsync();
            
        }



        public async Task HandleWithdrawalExecutedAsync(string status, BigInteger campaignId, BigInteger requestId, string recipient, BigInteger amount)
        {
            var campaign = await _investDbContext.Campaigns.FindAsync((int)campaignId);
            if (campaign == null) return;

            if (status == "Rejected")
            {
                campaign.DeniedWithdrawalRequestCount++;
            }

            await _investDbContext.SaveChangesAsync();
        }


        public async Task<bool> IsEventProcessedAsync(string transactionHash, string eventType)
        {
            return await _investDbContext.EventLogBlockchain
                .AnyAsync(e => e.TransactionHash == transactionHash && e.EventType == eventType);
        }

        public async Task LogEventAsync(string eventType, string transactionHash, int blockNumber, int campaignId, string eventData)
        {
            var eventLog = new EventLogBlockchain
            {
                EventType = eventType,
                TransactionHash = transactionHash,
                BlockNumber = blockNumber,
                CampaignId = campaignId,
                EventData = eventData,
                ProcessedAt = DateTime.UtcNow
            };
            EntityEntry<EventLogBlockchain> entityEntry = _investDbContext.EventLogBlockchain.Add(eventLog);
            await _investDbContext.SaveChangesAsync();
        }

        public async Task HandleWithdrawalRequestedAsync(BigInteger campaignId, BigInteger requestId, string requester, string txhash, BigInteger amount, string reason, DateTime voteEndTime, DateTime timeCreate)
        {
            var campaign = await _investDbContext.Campaigns.FindAsync((int)campaignId);
            if (campaign == null) return;
            var withdrawalRequest = new WithdrawalRequest
            {
                CampaignId = (int)campaignId,
                Id = (int)requestId,
                //Amount = (double)Web3.Convert.FromWei(amount),
                Reason = reason,
                txhash = txhash,
                Status = WithdrawalStatus.Pending,
                RequesterAddress = requester,
                VoteEndTime = voteEndTime,
                CreatedAt = timeCreate
            };
            _investDbContext.WithdrawalRequests.Add(withdrawalRequest);
            await _investDbContext.SaveChangesAsync();

        }
    }
}
