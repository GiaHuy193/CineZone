using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Models;

namespace WebMTB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class ShowtimesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShowtimesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var showtimes = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();

            return View(showtimes);
        }

        public async Task<IActionResult> Create()
        {
            await LoadDropdowns();

            return View(new Showtime
            {
                StartTime = DateTime.Now.AddDays(1).Date.AddHours(19).AddMinutes(30),
                BasePrice = 95000,
                IsHoliday = false
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Showtime showtime)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns();
                return View(showtime);
            }

            _context.Showtimes.Add(showtime);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo suất chiếu thành công.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);

            if (showtime == null)
            {
                return NotFound();
            }

            await LoadDropdowns();
            return View(showtime);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Showtime showtime)
        {
            if (id != showtime.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdowns();
                return View(showtime);
            }

            _context.Showtimes.Update(showtime);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật suất chiếu thành công.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (showtime == null)
            {
                return NotFound();
            }

            return View(showtime);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);

            if (showtime == null)
            {
                return NotFound();
            }

            bool hasTickets = await _context.Tickets.AnyAsync(t => t.ShowtimeId == id);

            if (hasTickets)
            {
                TempData["Error"] = "Không thể xóa suất chiếu đã có vé.";
                return RedirectToAction(nameof(Index));
            }

            _context.Showtimes.Remove(showtime);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa suất chiếu thành công.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadDropdowns()
        {
            ViewBag.Movies = await _context.Movies
                .OrderBy(m => m.Title)
                .ToListAsync();

            ViewBag.Rooms = await _context.Rooms
                .OrderBy(r => r.Name)
                .ToListAsync();
        }
    }
}