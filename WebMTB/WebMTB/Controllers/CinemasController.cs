using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;

namespace WebMTB.Controllers
{
    public class CinemasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CinemasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Cinemas
        public async Task<IActionResult> Index()
        {
            var cinemas = await _context.Cinemas
                .Include(c => c.Rooms)
                    .ThenInclude(r => r.Seats)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(cinemas);
        }
    }
}