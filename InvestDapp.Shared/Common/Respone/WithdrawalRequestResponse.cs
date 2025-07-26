using InvestDapp.Shared.Enums;

namespace InvestDapp.Shared.Common.Respone
{
    public class WithdrawalRequestResponse
    {
        public int Id { get; set; }
        public int CampaignId { get; set; }
        public CampaignBasicResponse? Campaign { get; set; }

        public int RequestIdOnChain { get; set; }
        public string RequesterAddress { get; set; }
        public string Reason { get; set; }
        public double Amount { get; set; }

        public WithdrawalStatus Status { get; set; }
        public decimal AgreeVotes { get; set; }
        public decimal DisagreeVotes { get; set; }
        public DateTime VoteEndTime { get; set; }

        public List<VoteResponse> Votes { get; set; } = new();
    }

}
