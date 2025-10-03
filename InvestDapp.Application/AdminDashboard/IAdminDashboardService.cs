using System.Threading;
using System.Threading.Tasks;

namespace InvestDapp.Application.AdminDashboard
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardData> GetDashboardAsync(int months = 6, CancellationToken cancellationToken = default);
    }
}
