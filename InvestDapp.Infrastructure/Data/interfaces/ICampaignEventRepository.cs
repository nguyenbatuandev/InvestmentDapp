using System.Numerics;


namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface ICampaignEventRepository
    {
        Task HandleCampaignCreatedAsync(BigInteger campaignId, string owner, string name, BigInteger goalAmount, BigInteger endTime , DateTime time);
        Task HandleInvestmentReceivedAsync(BigInteger campaignId, string investor, BigInteger amount, BigInteger currentRaisedAmount, string txHash , DateTime time);
        Task HandleProfitAddedAsync(BigInteger id, BigInteger campaignId, BigInteger amount , string txhash , DateTime time);
        Task HandleCampaignStatusUpdatedAsync(BigInteger campaignId, byte newStatus);
        Task HandleWithdrawalExecutedAsync(string status, BigInteger campaignId, BigInteger requestId, string recipient, BigInteger amount);
        Task HandleWithdrawalRequestedAsync(BigInteger campaignId, BigInteger requestId, string requester,string txhash, BigInteger amount, string reason, DateTime voteEndTime , DateTime timeCreate);
        Task HandleVoteCastAsync(BigInteger campaignId, BigInteger requestId, string voter, bool agree, BigInteger voteWeight, string txHash, DateTime time);
        Task<bool> IsEventProcessedAsync(string transactionHash, string eventType);
        Task LogEventAsync(string eventType, string transactionHash, int blockNumber, int campaignId, string eventData);
    }
}
