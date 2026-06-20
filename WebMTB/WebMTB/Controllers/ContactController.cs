using Microsoft.AspNetCore.Mvc;

namespace WebMTB.Controllers
{
    public class ContactController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}