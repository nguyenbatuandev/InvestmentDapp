using InvestDapp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.DTOs
{
    public class WithdrawalRequestDto
    {
        public int CampaignId { get; set; }
        public string TxHash { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string address { get; set; } = string.Empty;
    }

    public class RefundRequestDto
    {
        public int CampaignId { get; set; }
    }

    public class  ClaimRefundDto
    {
        public int CampaignId { get; set; }
        public string TransactionHash { get; set; } = string.Empty;
        public string InvestorAddress { get; set; } = string.Empty;
    }
}
