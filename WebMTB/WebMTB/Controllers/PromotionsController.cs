using Microsoft.AspNetCore.Mvc;

namespace WebMTB.Controllers
{
    public class PromotionsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}