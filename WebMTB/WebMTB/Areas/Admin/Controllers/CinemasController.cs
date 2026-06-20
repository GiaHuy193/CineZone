using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Models;

namespace WebMTB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class CinemasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CinemasController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var cinemas = await _context.Cinemas
                .Include(c => c.Rooms)
                    .ThenInclude(r => r.Seats)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(cinemas);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Cinema cinema)
        {
            if (!ModelState.IsValid)
            {
                return View(cinema);
            }

            _context.Cinemas.Add(cinema);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo rạp thành công.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var cinema = await _context.Cinemas.FindAsync(id);

            if (cinema == null)
            {
                return NotFound();
            }

            return View(cinema);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Cinema cinema)
        {
            if (id != cinema.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(cinema);
            }

            _context.Cinemas.Update(cinema);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật rạp thành công.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var cinema = await _context.Cinemas
                .Include(c => c.Rooms)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cinema == null)
            {
                return NotFound();
            }

            return View(cinema);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cinema = await _context.Cinemas
                .Include(c => c.Rooms)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cinema == null)
            {
                return NotFound();
            }

            if (cinema.Rooms.Any())
            {
                TempData["Error"] = "Không thể xóa rạp vì rạp này đã có phòng chiếu.";
                return RedirectToAction(nameof(Index));
            }

            _context.Cinemas.Remove(cinema);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa rạp thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}