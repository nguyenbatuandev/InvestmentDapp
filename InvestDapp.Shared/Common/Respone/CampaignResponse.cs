using InvestDapp.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Common.Respone
{
    public class CampaignResponse
    {
        public int Id { get; set; }
        public string OwnerAddress { get; set; }
        public string Name { get; set; }

        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }

        public int? CategoryId { get; set; }
        public CategoryResponse? Category { get; set; }

        public double GoalAmount { get; set; }
        public double CurrentRaisedAmount { get; set; }
        public double TotalInvestmentsOnCompletion { get; set; }
        public double TotalProfitAdded { get; set; }

        public DateTime EndTime { get; set; }
        public CampaignStatus Status { get; set; }
        public int InvestorCount { get; set; }
        public int DeniedWithdrawalRequestCount { get; set; }

        public List<InvestmentResponse> Investments { get; set; } = new();
        public List<WithdrawalRequestResponse> WithdrawalRequests { get; set; } = new();
        public List<ProfitResponse> Profits { get; set; } = new();
        public RefundResponse? Refund { get; set; }
    }

}
