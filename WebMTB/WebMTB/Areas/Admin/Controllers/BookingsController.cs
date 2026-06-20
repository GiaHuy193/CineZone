using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;

namespace WebMTB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? status = "All")
        {
            var bookingsQuery = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(b => b.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(b => b.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Room)
                            .ThenInclude(r => r.Cinema)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                bookingsQuery = bookingsQuery.Where(b => b.Status == status);
            }

            ViewBag.CurrentStatus = status ?? "All";

            ViewBag.TotalBookings = await _context.Bookings.CountAsync();
            ViewBag.PendingBookings = await _context.Bookings.CountAsync(b => b.Status == "Pending");
            ViewBag.CompletedBookings = await _context.Bookings.CountAsync(b => b.Status == "Completed");
            ViewBag.CancelledBookings = await _context.Bookings.CountAsync(b => b.Status == "Cancelled");

            ViewBag.TotalRevenue = await _context.Bookings
                .Where(b => b.Status == "Completed")
                .SumAsync(b => (decimal?)b.TotalAmount) ?? 0;

            var bookings = await bookingsQuery
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            return View(bookings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkCompleted(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);

            if (booking == null)
            {
                return NotFound();
            }

            if (booking.Status != "Pending")
            {
                TempData["Error"] = "Chỉ có thể chuyển booking Pending sang Completed.";
                return RedirectToAction(nameof(Index));
            }

            booking.Status = "Completed";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã chuyển booking sang Completed.";
            return RedirectToAction(nameof(Index), new { status = "Pending" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);

            if (booking == null)
            {
                return NotFound();
            }

            if (booking.Status != "Pending")
            {
                TempData["Error"] = "Chỉ có thể hủy booking đang Pending.";
                return RedirectToAction(nameof(Index));
            }

            booking.Status = "Cancelled";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã hủy booking.";
            return RedirectToAction(nameof(Index), new { status = "Pending" });
        }
    }
}