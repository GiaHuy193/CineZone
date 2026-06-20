using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Models;

namespace WebMTB.Areas.Admin.Controllers
{
    public class SeatCellDto
    {
        public int GridRow { get; set; }
        public int GridColumn { get; set; }
        public string SeatType { get; set; } = "Empty";
    }

    public class SaveSeatLayoutRequest
    {
        public int RoomId { get; set; }
        public int TotalRows { get; set; }
        public int TotalColumns { get; set; }
        public List<SeatCellDto> Seats { get; set; } = new();
    }

    [Area("Admin")]
    [Authorize]
    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/Rooms
        public async Task<IActionResult> Index(int? cinemaId)
        {
            var query = _context.Rooms
                .Include(r => r.Cinema)
                .Include(r => r.Seats)
                .AsQueryable();

            if (cinemaId.HasValue)
            {
                query = query.Where(r => r.CinemaId == cinemaId.Value);
            }

            var rooms = await query
                .OrderBy(r => r.Cinema!.Name)
                .ThenBy(r => r.Name)
                .ToListAsync();

            return View(rooms);
        }

        // GET: /Admin/Rooms/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Cinemas = await _context.Cinemas
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View();
        }

        // POST: /Admin/Rooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Room room)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Cinemas = await _context.Cinemas
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return View(room);
            }

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo phòng chiếu thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Rooms/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Cinema)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (room == null)
            {
                return NotFound();
            }

            var seats = await _context.Seats
                .Where(s => s.RoomId == id)
                .OrderBy(s => s.GridRow)
                .ThenBy(s => s.GridColumn)
                .ToListAsync();

            ViewBag.Seats = seats;

            return View(room);
        }

        // GET: /Admin/Rooms/SeatDesigner/5
        public async Task<IActionResult> SeatDesigner(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Cinema)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (room == null)
            {
                return NotFound();
            }

            var seats = await _context.Seats
                .Where(s => s.RoomId == id)
                .OrderBy(s => s.GridRow)
                .ThenBy(s => s.GridColumn)
                .ToListAsync();

            ViewBag.Seats = seats;

            return View(room);
        }

        // POST: /Admin/Rooms/SaveSeatLayout
        [HttpPost]
        public async Task<IActionResult> SaveSeatLayout([FromBody] SaveSeatLayoutRequest request)
        {
            if (request == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu gửi lên không hợp lệ."
                });
            }

            var room = await _context.Rooms.FindAsync(request.RoomId);

            if (room == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Không tìm thấy phòng chiếu."
                });
            }

            bool hasTickets = await _context.Tickets
                .AnyAsync(t => t.Seat != null && t.Seat.RoomId == request.RoomId);

            if (hasTickets)
            {
                return Json(new
                {
                    success = false,
                    message = "Phòng này đã có vé được đặt, không thể sửa sơ đồ ghế."
                });
            }

            var oldSeats = await _context.Seats
                .Where(s => s.RoomId == request.RoomId)
                .ToListAsync();

            if (oldSeats.Any())
            {
                _context.Seats.RemoveRange(oldSeats);
            }

            var validSeats = request.Seats
                .Where(s => s.SeatType != "Empty")
                .OrderBy(s => s.GridRow)
                .ThenBy(s => s.GridColumn)
                .ToList();

            var rowCounters = new Dictionary<int, int>();

            foreach (var cell in validSeats)
            {
                if (!rowCounters.ContainsKey(cell.GridRow))
                {
                    rowCounters[cell.GridRow] = 0;
                }

                rowCounters[cell.GridRow]++;

                string rowName = GetRowName(cell.GridRow);
                int seatNumber = rowCounters[cell.GridRow];

                _context.Seats.Add(new Seat
                {
                    RoomId = request.RoomId,
                    Row = rowName,
                    Number = seatNumber,
                    SeatType = cell.SeatType,
                    GridRow = cell.GridRow,
                    GridColumn = cell.GridColumn
                });
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Lưu sơ đồ ghế thành công."
            });
        }

        // POST: /Admin/Rooms/GenerateSeats
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateSeats(int roomId)
        {
            var room = await _context.Rooms.FindAsync(roomId);

            if (room == null)
            {
                return NotFound();
            }

            bool hasSeats = await _context.Seats.AnyAsync(s => s.RoomId == roomId);

            if (hasSeats)
            {
                TempData["Error"] = "Phòng này đã có ghế rồi. Nếu muốn sửa, hãy dùng chức năng Thiết kế sơ đồ ghế.";
                return RedirectToAction(nameof(Details), new { id = roomId });
            }

            string[] rows = { "A", "B", "C", "D", "E", "F" };

            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                string row = rows[rowIndex];

                for (int number = 1; number <= 10; number++)
                {
                    _context.Seats.Add(new Seat
                    {
                        Row = row,
                        Number = number,
                        SeatType = "Normal",
                        GridRow = rowIndex + 1,
                        GridColumn = number,
                        RoomId = roomId
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã tạo nhanh ghế A-F, mỗi hàng 10 ghế.";
            return RedirectToAction(nameof(Details), new { id = roomId });
        }

        private string GetRowName(int rowIndex)
        {
            string result = "";

            while (rowIndex > 0)
            {
                rowIndex--;
                result = (char)('A' + rowIndex % 26) + result;
                rowIndex /= 26;
            }

            return result;
        }

        // GET: /Admin/Rooms/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Cinema)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (room == null)
            {
                return NotFound();
            }

            ViewBag.Cinemas = await _context.Cinemas
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(room);
        }

        // POST: /Admin/Rooms/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Room room)
        {
            if (id != room.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Cinemas = await _context.Cinemas
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return View(room);
            }

            var existingRoom = await _context.Rooms.FindAsync(id);

            if (existingRoom == null)
            {
                return NotFound();
            }

            existingRoom.Name = room.Name;
            existingRoom.CinemaId = room.CinemaId;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật phòng chiếu thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}