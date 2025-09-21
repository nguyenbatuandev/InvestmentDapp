using InvestDapp.Infrastructure.Data.interfaces;
using Microsoft.Extensions.Logging;
using InvestDapp.Models;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Models.BlockchainModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Nethereum.Web3;
using System.Numerics;
using System.Globalization;
namespace InvestDapp.Infrastructure.Data.Repository
{
    public class CampaignEventRepository : ICampaignEventRepository
    {
        private readonly InvestDbContext _investDbContext;
        private readonly ILogger<CampaignEventRepository>? _logger;

        public CampaignEventRepository(InvestDbContext investDbContext, ILogger<CampaignEventRepository>? logger = null)
        {
            _investDbContext = investDbContext;
            _logger = logger;
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
            if (campaign == null) return;

            // Solidity enum: enum CampaignStatus { Active, Voting, Completed, Failed }
            // numeric values: 0 = Active, 1 = Voting, 2 = Completed, 3 = Failed
            // App enum (InvestDapp.Shared.Enums.CampaignStatus) has a different ordering.
            // Map Solidity values explicitly to the app's CampaignStatus values to avoid incorrect translations.

            CampaignStatus mappedStatus;
            switch (newStatus)
            {
                case 0: // Active
                    mappedStatus = CampaignStatus.Active;
                    break;
                case 1: // Voting
                    mappedStatus = CampaignStatus.Voting;
                    break;
                case 2: // Completed
                    mappedStatus = CampaignStatus.Completed;
                    break;
                case 3: // Failed
                    mappedStatus = CampaignStatus.Failed;
                    break;
                default:
                    // Unknown status received from chain; ignore
                    return;
            }

            // Only update and save when status actually changes to avoid unintended overwrites
            if (campaign.Status != mappedStatus)
            {
                campaign.Status = mappedStatus;

                if (mappedStatus == CampaignStatus.Completed)
                {
                    campaign.TotalInvestmentsOnCompletion = campaign.CurrentRaisedAmount;
                }

                await _investDbContext.SaveChangesAsync();
            }
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
            _logger?.LogInformation("WithdrawalRequested persisted: campaignId={CampaignId} requestId={RequestId} tx={Tx}", campaignId, requestId, txhash);

        }

        public async Task HandleVoteCastAsync(BigInteger campaignId, BigInteger requestId, string voter, bool agree, BigInteger voteWeight, string txHash, DateTime time)
        {
            try
            {
                _logger?.LogInformation("HandleVoteCastAsync called: campaignId={CampaignId} requestId={RequestId} voter={Voter} agree={Agree} weight={Weight} tx={Tx}", campaignId, requestId, voter, agree, voteWeight, txHash);

                // Find the withdrawal request
                var wr = await _investDbContext.WithdrawalRequests
                    .Include(w => w.Votes)
                    .FirstOrDefaultAsync(w => w.CampaignId == (int)campaignId && w.Id == (int)requestId);

                if (wr == null)
                {
                    // If we don't have the withdrawal request yet, create a minimal placeholder so we can store the vote
                    wr = new WithdrawalRequest
                    {
                        CampaignId = (int)campaignId,
                        Id = (int)requestId,
                        Reason = "(on-chain)",
                        txhash = txHash,
                        Status = WithdrawalStatus.Pending,
                        VoteEndTime = DateTime.UtcNow,
                        CreatedAt = time
                    };
                    _investDbContext.WithdrawalRequests.Add(wr);
                    await _investDbContext.SaveChangesAsync();
                    _logger?.LogInformation("Created placeholder WithdrawalRequest: campaignId={CampaignId} requestId={RequestId}", campaignId, requestId);
                }

                // Idempotency: ensure we haven't already recorded this tx for the same voter and request
                // EF Core can't translate String.Equals with StringComparison overload into SQL. Normalize addresses to lower-case for comparison which EF can translate.
                var normalizedVoter = (voter ?? string.Empty).ToLowerInvariant();
                var already = await _investDbContext.Vote.AnyAsync(v => v.WithdrawalRequestId == wr.Id && (v.TransactionHash == txHash || (v.VoterAddress != null && v.VoterAddress.ToLower() == normalizedVoter)));
                if (already)
                {
                    _logger?.LogInformation("Vote already exists or voter already recorded: campaignId={CampaignId} requestId={RequestId} voter={Voter} tx={Tx}", campaignId, requestId, voter, txHash);
                    return;
                }

                var vote = new Vote
                {
                    TransactionHash = txHash,
                    WithdrawalRequestId = wr.Id,
                    VoterAddress = voter ?? string.Empty,
                    Agreed = agree,
                    VoteWeight = (double)Web3.Convert.FromWei(voteWeight),
                    CreatedAt = time
                };

                _investDbContext.Vote.Add(vote);

                // Update tallies on WithdrawalRequest
                // voteWeight is a BigInteger (wei). Convert to decimal safely for DB column decimal(38,0).
                decimal votesToAdd = 0m;
                try
                {
                    // Parse via invariant string representation to avoid IConvertible cast issues
                    votesToAdd = decimal.Parse(voteWeight.ToString(), NumberStyles.None, CultureInfo.InvariantCulture);
                }
                catch (Exception convEx)
                {
                    _logger?.LogError(convEx, "Failed to convert BigInteger voteWeight to decimal for tx={Tx} campaignId={CampaignId} requestId={RequestId}", txHash, campaignId, requestId);
                    // If conversion fails, do not throw; default to 0 to avoid corrupting totals. This case should be extremely rare.
                    votesToAdd = 0m;
                }

                if (agree)
                {
                    // store as decimal (wei) to match DB column type
                    wr.AgreeVotes += votesToAdd;
                }
                else
                {
                    wr.DisagreeVotes += votesToAdd;
                }

                _logger?.LogInformation("Saving vote to DB: campaignId={CampaignId} requestId={RequestId} voteIdCandidateTx={Tx}", campaignId, requestId, txHash);
                try
                {
                    // Log current WR tallies before saving for diagnostics
                    _logger?.LogDebug("WithdrawalRequest before save: Id={WrId} AgreeVotes={Agree} DisagreeVotes={Disagree}", wr.Id, wr.AgreeVotes, wr.DisagreeVotes);
                    await _investDbContext.SaveChangesAsync();
                    _logger?.LogInformation("Vote persisted: campaignId={CampaignId} requestId={RequestId} tx={Tx}", campaignId, requestId, txHash);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger?.LogError(dbEx, "DbUpdateException while saving Vote for tx={Tx}. WrId={WrId} voter={Voter} agree={Agree}", txHash, wr.Id, voter, agree);
                    if (dbEx.Entries != null)
                    {
                        foreach (var entry in dbEx.Entries)
                        {
                            try
                            {
                                _logger?.LogError("Entity in error: {EntityType} State={State}", entry.Entity?.GetType().FullName, entry.State);
                            }
                            catch { }
                        }
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Unexpected error when saving vote for tx={Tx}", txHash);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while handling VoteCast: campaignId={CampaignId} requestId={RequestId} tx={Tx}", campaignId, requestId, txHash);
                throw; // rethrow so outer transaction can catch and rollback and logs will capture it
            }
        }
    }
}
