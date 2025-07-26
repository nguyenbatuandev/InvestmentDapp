using Microsoft.AspNetCore.Mvc;

namespace InvestDapp.Controllers
{
    public class CampaignsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
