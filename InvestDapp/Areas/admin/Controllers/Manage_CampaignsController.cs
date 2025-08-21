using Microsoft.AspNetCore.Mvc;

namespace InvestDapp.Areas.admin.Controllers
{
    public class Manage_CampaignsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
