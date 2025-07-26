using Microsoft.AspNetCore.Mvc;

namespace InvestDapp.Controllers
{
    public class NewsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
