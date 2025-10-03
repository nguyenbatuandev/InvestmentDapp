using InvestDapp.Shared.Common.Request;
using System.Threading.Tasks;

namespace InvestDapp.Application.AdminAnalytics
{
    public interface ITransactionReportPdfService
    {
        Task<byte[]> GenerateReportAsync(TransactionReportFilterRequest filterRequest);
    }
}
