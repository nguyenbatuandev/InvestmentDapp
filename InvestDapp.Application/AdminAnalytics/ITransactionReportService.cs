using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.DTOs.Admin;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InvestDapp.Application.AdminAnalytics
{
    public interface ITransactionReportService
    {
        Task<TransactionReportResultDto> GetTransactionsAsync(TransactionReportFilterRequest filterRequest);
        Task<IReadOnlyList<string>> GetCampaignNamesAsync();
    }
}
