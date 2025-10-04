using InvestDapp.Application.AdminDashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using InvestDapp.Shared.Security;

namespace InvestDapp.Areas.admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaffAccess)] // CHANGED: Allow all staff roles to view dashboard
    [Route("admin/[controller]")]
    public class DashboardController : Controller
    {
        private readonly IAdminDashboardService _dashboardService;

        public DashboardController(IAdminDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index()
        {
            var data = await _dashboardService.GetDashboardAsync();
            ViewData["Title"] = "Dashboard";
            return View(data);
        }
    }
}
