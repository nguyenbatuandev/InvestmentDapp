using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.DTOs.Admin;
using System.Collections.Generic;

namespace InvestDapp.Areas.admin.ViewModels
{
    public class TransactionReportPageViewModel
    {
        public TransactionReportFilterRequest Filter { get; set; } = new();
        public TransactionReportResultDto Result { get; set; } = new();
        public IReadOnlyList<string> CampaignOptions { get; set; } = new List<string>();
    }
}
