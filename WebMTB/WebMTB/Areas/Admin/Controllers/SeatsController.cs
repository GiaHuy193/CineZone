using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Models;

namespace WebMTB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class SeatsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SeatsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? roomId)
        {
            var rooms = await _context.Rooms
                .Include(r => r.Cinema)
                .OrderBy(r => r.Name)
                .ToListAsync();

            ViewBag.Rooms = rooms;

            if (!rooms.Any())
            {
                ViewBag.CurrentRoomId = null;
                ViewBag.CurrentRoom = null;
                return View(new List<Seat>());
            }

            // Nếu chưa chọn phòng thì lấy phòng đầu tiên
            int selectedRoomId = roomId ?? rooms.First().Id;

            var currentRoom = rooms.FirstOrDefault(r => r.Id == selectedRoomId);

            var seats = await _context.Seats
                .Where(s => s.RoomId == selectedRoomId)
                .OrderBy(s => s.GridRow)
                .ThenBy(s => s.GridColumn)
                .ToListAsync();

            ViewBag.CurrentRoomId = selectedRoomId;
            ViewBag.CurrentRoom = currentRoom;

            return View(seats);
        }
    }
}