
using InvestDapp.Application.MessageService;
using InvestDapp.Application.NotificationService;
using InvestDapp.Application.UserService;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models.BlockchainModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Text.Json;
using static InvestDapp.Shared.DTOs.EvenBockchainDTO;

namespace Invest.Application.EventService
{
    public class CampaignEventService
    {
        private readonly Web3 _web3;
        private readonly InvestDbContext _dbContext;
        private readonly ICampaignEventRepository _eventRepository;
        private readonly ILogger<CampaignEventService> _logger;
        private readonly BlockchainConfig _config;
        private readonly IUserService _userService;
        private readonly IConversationService _conversationService;
        private readonly INotificationService _notificationService;
        public CampaignEventService(
            Web3 web3,
            InvestDbContext dbContext,
            ICampaignEventRepository eventRepository,
            ILogger<CampaignEventService> logger,
            IOptions<BlockchainConfig> config,
            IConversationService conversationService,
            IUserService userService,
            INotificationService notificationService)
        {
            _web3 = web3;
            _dbContext = dbContext;
            _eventRepository = eventRepository;
            _logger = logger;
            _config = config.Value;
            _conversationService = conversationService;
            _userService = userService;
            _notificationService = notificationService;
        }

        public async Task ProcessNewEventsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // 1. Lấy block mới nhất và block đã xử lý
                var currentBlockNumber = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                var lastProcessedBlock = await GetLastProcessedBlockAsync();
                var safeBlockNumber = (long)currentBlockNumber.Value - _config.BlockConfirmations;

                if (lastProcessedBlock >= safeBlockNumber)
                {
                    _logger.LogInformation("No new safe blocks to process. Last processed: {LastBlock}", lastProcessedBlock);
                    return;
                }

                // Toàn bộ phạm vi cần xử lý trong lần chạy này
                var fromBlock = lastProcessedBlock + 1;
                var toBlock = safeBlockNumber;

                _logger.LogInformation("Total range to process: {FromBlock} to {ToBlock}", fromBlock, toBlock);

                long currentChunkStartBlock = fromBlock;
                while (currentChunkStartBlock <= toBlock)
                {
                    // ... (phần code chia chunk giữ nguyên) ...
                    long currentChunkEndBlock = Math.Min(toBlock, currentChunkStartBlock + _config.MaxBlockRange - 1);

                    _logger.LogInformation("--> Processing chunk from block {Start} to {End}", currentChunkStartBlock, currentChunkEndBlock);

                    // 2. THÊM ĐOẠN CODE LẮNG NGHE INVESTMENT TẠI ĐÂY
                    await ProcessEventsInRange<InvestmentReceivedEventDTO>(currentChunkStartBlock, currentChunkEndBlock, "InvestmentReceived", async (log) =>
                    {
                        var evt = log.Event;
                        var txHash = log.Log.TransactionHash;
                        // Lấy thông tin về transaction
                        var transaction = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHash);

                        // Lấy thông tin block chứa transaction này
                        var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(transaction.BlockNumber);

                        // Lấy timestamp của block
                        var timestamp = block.Timestamp;  // Đơn vị là seconds từ Unix epoch
                        DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds((long)timestamp.Value).UtcDateTime;

                        var condersationID = await _dbContext.Conversations
                            .FirstOrDefaultAsync(x => x.CampaignId == (int)evt.CampaignId);
                        var user = await _dbContext.Users
                            .FirstOrDefaultAsync(x => x.WalletAddress == evt.Investor.ToString());

                        var alreadyJoined = await _dbContext.Participants
                            .AnyAsync(p => p.ConversationId == condersationID.ConversationId && p.UserId == user.ID);

                        if (!alreadyJoined)
                        {
                            await _conversationService.AddMemberToGroupAsync(condersationID.ConversationId, user.ID, user.ID);
                        }

                        // Gọi repository để cập nhật DB
                        await _eventRepository.HandleInvestmentReceivedAsync(evt.CampaignId, evt.Investor, evt.Amount, evt.CurrentRaisedAmount,txHash, dateTime);
                       
                        // Ghi log sự kiện đã xử lý
                        await _eventRepository.LogEventAsync("InvestmentReceived", log.Log.TransactionHash, (int)log.Log.BlockNumber.Value, (int)evt.CampaignId, JsonSerializer.Serialize(evt));
                        var noti = new CreateNotificationRequest
                        {
                            UserId = user.ID,
                            Type = "InvestmentReceived",
                            Title = "Đầu tư thành công",
                            Message = $"Bạn đã đầu tư {evt.Amount} vào chiến dịch ID {evt.CampaignId}. Cảm ơn bạn đã đồng hành cùng chúng tôi!",
                        };
                        var notifyResult = await _notificationService.CreateNotificationAsync(noti);


                    }, cancellationToken);


                    // 3. Xử lý WithdrawalRequestCreated
                    await ProcessEventsInRange<ProfitAddedEventDTO>(currentChunkStartBlock, currentChunkEndBlock, "ProfitAdded", async (log) =>
                    {
                        var evt = log.Event;
                        var txHash = log.Log.TransactionHash;
                        //var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(log.Log.BlockNumber);
                        //var time = block.Timestamp;
                        //DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds((long)time.Value).UtcDateTime;

                        // Lấy thông tin về transaction
                        var transaction = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHash);

                        // Lấy thông tin block chứa transaction này
                        var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(transaction.BlockNumber);

                        // Lấy timestamp của block
                        var timestamp = block.Timestamp;  // Đơn vị là seconds từ Unix epoch
                        DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds((long)timestamp.Value).UtcDateTime;

                        await _eventRepository.HandleProfitAddedAsync(evt.Id ,evt.CampaignId, evt.Amount , txHash , dateTime);

                        await _eventRepository.LogEventAsync("ProfitAdded", txHash, (int)log.Log.BlockNumber.Value, (int)evt.CampaignId, JsonSerializer.Serialize(evt));
                    }, cancellationToken);


                    // 4. Xử lý WithdrawalRequestCreated
                    await ProcessEventsInRange<CampaignStatusUpdatedEventDTO>(currentChunkStartBlock, currentChunkEndBlock, "CampaignStatusUpdated", async (log) =>
                    {
                        var evt = log.Event;
                        
                        await _eventRepository.HandleCampaignStatusUpdatedAsync(evt.CampaignId, evt.NewStatus);

                    }, cancellationToken);

                    // 5. Xử lý WithdrawalRequestCreated
                    await ProcessEventsInRange<WithdrawalRequestedEventDTO>(currentChunkStartBlock, currentChunkEndBlock, "WithdrawalRequested", async (log) =>
                    {
                        var evt = log.Event;
                        var txHash = log.Log.TransactionHash;
                        //BigInteger campaignId, BigInteger requestId, string requester, string txhash, BigInteger amount, string reason, BigInteger voteEndTime

                        var transaction = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHash);
                        // Lấy thông tin block chứa transaction này
                        var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(transaction.BlockNumber);

                        // Lấy timestamp của block
                        var timestamp = block.Timestamp;  // Đơn vị là seconds từ Unix epoch
                        DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds((long)timestamp.Value).UtcDateTime;
                        DateTime voteEntime = DateTimeOffset.FromUnixTimeSeconds((long)evt.VoteEndTime).UtcDateTime;
                        await _eventRepository.HandleWithdrawalRequestedAsync(evt.CampaignId, evt.RequestId, evt.Requester, txHash, evt.Amount, evt.Reason, voteEntime, dateTime);


                    }, cancellationToken);

                    // 6. Xử lý VoteCast
                    await ProcessEventsInRange<VoteCastEventDTO>(currentChunkStartBlock, currentChunkEndBlock, "VoteCast", async (log) =>
                    {
                        var evt = log.Event;
                        var txHash = log.Log.TransactionHash;

                        // Lấy thông tin về transaction
                        var transaction = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHash);

                        // Lấy thông tin block chứa transaction này
                        var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(transaction.BlockNumber);

                        // Lấy timestamp của block
                        var timestamp = block.Timestamp;  // Đơn vị là seconds từ Unix epoch
                        DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds((long)timestamp.Value).UtcDateTime;

                        await _eventRepository.HandleVoteCastAsync(evt.CampaignId, evt.RequestId, evt.Voter, evt.Agree, evt.VoteWeight, txHash, dateTime);

                        await _eventRepository.LogEventAsync("VoteCast", txHash, (int)log.Log.BlockNumber.Value, (int)evt.CampaignId, JsonSerializer.Serialize(evt));

                    }, cancellationToken);

                    // Cập nhật block cuối cùng đã xử lý
                    await UpdateLastProcessedBlockAsync(currentChunkEndBlock);


                    // 5. Cập nhật block cuối cùng đã xử lý
                    await UpdateLastProcessedBlockAsync(currentChunkEndBlock);

                    _logger.LogInformation("--> Finished chunk. Last processed block is now {Block}", currentChunkEndBlock);

                    currentChunkStartBlock = currentChunkEndBlock + 1;
                }
            }
            catch (Exception ex)
            {
                // Ngoại lệ này vẫn quan trọng để biết nếu có lỗi không lường trước
                _logger.LogError(ex, "An error occurred during event processing loop.");
            }
        }
        private async Task ProcessEventsInRange<TEventDTO>(long fromBlock, long toBlock, string eventName, Func<EventLog<TEventDTO>, Task> handler, CancellationToken cancellationToken
        ) where TEventDTO : class, IEventDTO, new()
        {
            var eventHandler = _web3.Eth.GetEvent<TEventDTO>(_config.ContractAddress);
            var filter = eventHandler.CreateFilterInput(new BlockParameter((ulong)fromBlock), new BlockParameter((ulong)toBlock));
            var logs = await eventHandler.GetAllChangesAsync(filter);

            _logger.LogInformation("Found {Count} '{EventName}' log(s) in range {From}-{To}", logs?.Count ?? 0, eventName, fromBlock, toBlock);

            if (logs == null || logs.Count == 0)
            {
                _logger.LogInformation("No logs returned for event {EventName} in range {From}-{To}", eventName, fromBlock, toBlock);
                return;
            }

            foreach (var log in logs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var txHash = log.Log.TransactionHash;

                // Log compact payload for debugging
                try
                {
                    var payload = JsonSerializer.Serialize(log.Event);
                    _logger.LogDebug("Event payload for {EventName} tx={Tx}: {Payload}", eventName, txHash, payload);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to serialize event payload for {EventName} tx={Tx}", eventName, txHash);
                }

                if (await _eventRepository.IsEventProcessedAsync(txHash, eventName))
                {
                    _logger.LogWarning("Event with TxHash {TxHash} has already been processed. Skipping.", txHash);
                    continue;
                }

                // Bọc TOÀN BỘ logic xử lý trong một transaction
                using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    // Gọi handler để thực hiện tất cả công việc
                    await handler(log);

                    await dbTransaction.CommitAsync(cancellationToken);
                    _logger.LogInformation("Successfully processed event {EventName} from TxHash {TxHash}", eventName, txHash);
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    // Thay vì InvalidCastException, giờ bạn sẽ thấy lỗi gốc nếu có (ví dụ lỗi DB)
                    _logger.LogError(ex, "Failed to process event {EventName} from TxHash {TxHash}. Rolled back.", eventName, txHash);
                    // Không ném lại lỗi để vòng lặp có thể tiếp tục với các sự kiện khác
                }
            }
        }

        private async Task<long> GetLastProcessedBlockAsync()
        {
            var state = await _dbContext.EventProcessingStates
                .FirstOrDefaultAsync(s => s.ContractAddress == _config.ContractAddress);

            if (state != null)
            {
                return state.LastProcessedBlock;
            }

            // Nếu chưa có trạng thái nào trong DB (lần đầu chạy),
            // hãy bắt đầu từ block ngay trước block contract được deploy.
            // Trừ đi 1 vì vòng lặp chính sẽ cộng 1 vào.
            var startBlock = _config.ContractDeploymentBlock > 0 ? _config.ContractDeploymentBlock - 1 : 0;

            _logger.LogInformation("No previous state found. Starting scan from deployment block: {Block}", _config.ContractDeploymentBlock);

            return startBlock;
        }

        private async Task UpdateLastProcessedBlockAsync(long blockNumber)
        {
            var state = await _dbContext.EventProcessingStates
                .FirstOrDefaultAsync(s => s.ContractAddress == _config.ContractAddress);

            if (state == null)
            {
                state = new EventProcessingState { ContractAddress = _config.ContractAddress };
                _dbContext.EventProcessingStates.Add(state);
            }

            state.LastProcessedBlock = blockNumber;
            state.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }
}
