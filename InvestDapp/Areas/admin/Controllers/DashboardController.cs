using Microsoft.AspNetCore.Mvc;

namespace InvestDapp.Areas.admin.Controllers
{
    [Area("Admin")]
    [Route("admin/[controller]/[action]")]  
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
