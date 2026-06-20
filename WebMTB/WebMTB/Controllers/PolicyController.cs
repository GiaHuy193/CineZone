using Microsoft.AspNetCore.Mvc;

namespace WebMTB.Controllers
{
    public class PolicyController : Controller
    {
        public IActionResult BookingGuide()
        {
            return View();
        }

        public IActionResult Payment()
        {
            return View();
        }

        public IActionResult FAQ()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Member()
        {
            return View();
        }

        public IActionResult CinemaRules()
        {
            return View();
        }
    }
}