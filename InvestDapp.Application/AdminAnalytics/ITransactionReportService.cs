using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.DTOs.Admin;
using InvestDapp.Shared.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InvestDapp.Application.AdminAnalytics
{
    public interface ITransactionReportService
    {
        Task<TransactionReportResultDto> GetTransactionsAsync(TransactionReportFilterRequest filterRequest);
        Task<IReadOnlyList<string>> GetCampaignNamesAsync();
        Task<TransactionChartDataDto> GetChartDataAsync(TransactionReportFilterRequest filterRequest, TransactionGrouping grouping, int topCampaigns = 5);
    }
}
