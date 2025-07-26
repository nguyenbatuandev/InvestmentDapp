

namespace InvestDapp.Shared.Common.Respone
{
    public class InvestmentResponse
    {
        public int Id { get; set; }
        public string TransactionHash { get; set; }

        public int CampaignId { get; set; }
        public string InvestorAddress { get; set; }
        public double Amount { get; set; }
        public DateTime Timestamp { get; set; }

        //public CampaignBasicResponse? Campaign { get; set; }
    }

}
