using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestDapp.Controllers
{
    
    [Route("TradingView")]
    [Route("Trading")]
    [Authorize]
    public class TradingViewController : Controller
    {
        // GET: TradingView/Chart hoặc Trading/Chart
        [Route("Chart")]
        public IActionResult Chart(string symbol = "BTCUSDT")
        {
            ViewBag.Symbol = symbol;
            return View();
        }

        // GET: TradingView/Markets hoặc Trading/Markets
        [Route("Markets")]
        public IActionResult Markets()
        {
            return View();
        }

        // GET: TradingView/Portfolio hoặc Trading/Portfolio
        [Route("Portfolio")]
        public IActionResult Portfolio()
        {
            return View();
        }
    }
}