using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebMTB.Data;
using WebMTB.Hubs;
using WebMTB.Models;
using WebMTB.Service;

namespace WebMTB.Controllers
{
    [Authorize]
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<TicketHub> _ticketHub;
        private readonly PayPalService _payPalService;

        private const int HoldMinutes = 5;

        public BookingsController(
            ApplicationDbContext context,
            IHubContext<TicketHub> ticketHub,
            PayPalService payPalService)
        {
            _context = context;
            _ticketHub = ticketHub;
            _payPalService = payPalService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var bookings = await _context.Bookings
                .Where(b => b.UserId == userId)
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
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            return View(bookings);
        }

        public async Task<IActionResult> Payments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var bookings = await _context.Bookings
                .Where(b => b.UserId == userId)
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
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            return View(bookings);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var booking = await _context.Bookings
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
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            if (IsExpiredPending(booking))
            {
                var expiredInfo = await ExpireBookingAsync(booking);

                TempData["Error"] = "Thời gian giữ ghế đã hết. Vui lòng chọn ghế lại.";

                if (expiredInfo.MovieId.HasValue && expiredInfo.ShowtimeId > 0)
                {
                    return RedirectToAction(nameof(Create), new
                    {
                        movieId = expiredInfo.MovieId.Value,
                        showtimeId = expiredInfo.ShowtimeId
                    });
                }

                return RedirectToAction("Index", "Home");
            }

            return View(booking);
        }

        public async Task<IActionResult> Create(int? movieId, int? showtimeId, DateTime? selectedDate)
        {
            if (movieId == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .FirstOrDefaultAsync(m => m.Id == movieId);

            if (movie == null)
            {
                return NotFound();
            }

            var now = DateTime.Now;
            var today = DateTime.Today;
            var weekEnd = today.AddDays(7);

            var weekDates = Enumerable.Range(0, 7)
                .Select(i => today.AddDays(i))
                .ToList();

            var selectedDay = selectedDate?.Date ?? today;

            if (selectedDay < today || selectedDay >= weekEnd)
            {
                selectedDay = today;
            }

            var showtimes = await _context.Showtimes
                .Include(s => s.Room)
                    .ThenInclude(r => r.Cinema)
                .Where(s =>
                    s.MovieId == movie.Id &&
                    s.StartTime >= today &&
                    s.StartTime < weekEnd)
                .OrderBy(s => s.Room!.Cinema!.Name)
                .ThenBy(s => s.Room!.Name)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            Showtime? showtime = null;

            if (showtimeId.HasValue)
            {
                showtime = showtimes.FirstOrDefault(s => s.Id == showtimeId.Value);

                if (showtime != null)
                {
                    selectedDay = showtime.StartTime.Date;
                }
            }

            if (showtime == null)
            {
                showtime = showtimes
                    .Where(s => s.StartTime.Date == selectedDay)
                    .OrderBy(s => s.Room!.Cinema!.Name)
                    .ThenBy(s => s.Room!.Name)
                    .ThenBy(s => s.StartTime)
                    .FirstOrDefault();
            }

            List<Seat> seats = new();
            List<int> soldSeatIds = new();
            List<int> heldSeatIds = new();

            await CleanupExpiredHoldsAsync(now);

            if (showtime != null)
            {
                await CleanupExpiredPendingBookingsByShowtimeAsync(showtime.Id, now);

                if (!string.IsNullOrEmpty(userId))
                {
                    var existingPendingBooking = await _context.Bookings
                        .Include(b => b.Tickets)
                        .Where(b =>
                            b.UserId == userId &&
                            b.Status == "Pending" &&
                            b.ExpiresAt != null &&
                            b.ExpiresAt > now &&
                            b.Tickets.Any(t => t.ShowtimeId == showtime.Id))
                        .OrderByDescending(b => b.BookingDate)
                        .FirstOrDefaultAsync();

                    if (existingPendingBooking != null)
                    {
                        TempData["Error"] = "Bạn đang có booking chờ thanh toán cho suất chiếu này. Vui lòng tiếp tục thanh toán hoặc hủy giữ ghế.";

                        return RedirectToAction(nameof(Checkout), new
                        {
                            id = existingPendingBooking.Id
                        });
                    }
                }

                var hasSeats = await _context.Seats.AnyAsync(s => s.RoomId == showtime.RoomId);

                if (!hasSeats)
                {
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
                                RoomId = showtime.RoomId
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                if (!string.IsNullOrEmpty(userId))
                {
                    var currentUserHolds = await _context.SeatHolds
                        .Where(h => h.ShowtimeId == showtime.Id && h.UserId == userId)
                        .ToListAsync();

                    if (currentUserHolds.Any())
                    {
                        _context.SeatHolds.RemoveRange(currentUserHolds);
                        await _context.SaveChangesAsync();
                    }
                }

                var oldGridSeats = await _context.Seats
                    .Where(s => s.RoomId == showtime.RoomId && (s.GridRow == 0 || s.GridColumn == 0))
                    .ToListAsync();

                if (oldGridSeats.Any())
                {
                    foreach (var seat in oldGridSeats)
                    {
                        if (!string.IsNullOrWhiteSpace(seat.Row))
                        {
                            seat.GridRow = char.ToUpper(seat.Row[0]) - 'A' + 1;
                        }

                        seat.GridColumn = seat.Number;
                    }

                    await _context.SaveChangesAsync();
                }

                seats = await _context.Seats
                    .Where(s => s.RoomId == showtime.RoomId)
                    .OrderBy(s => s.GridRow)
                    .ThenBy(s => s.GridColumn)
                    .ToListAsync();

                soldSeatIds = await _context.Tickets
                    .Where(t =>
                        t.ShowtimeId == showtime.Id &&
                        t.Booking != null &&
                        t.Booking.Status == "Completed")
                    .Select(t => t.SeatId)
                    .ToListAsync();

                var holdSeatIds = await _context.SeatHolds
                    .Where(h =>
                        h.ShowtimeId == showtime.Id &&
                        h.ExpiresAt > now &&
                        h.UserId != userId)
                    .Select(h => h.SeatId)
                    .ToListAsync();

                var pendingSeatIds = await _context.Tickets
                    .Where(t =>
                        t.ShowtimeId == showtime.Id &&
                        t.Booking != null &&
                        t.Booking.Status == "Pending" &&
                        t.Booking.ExpiresAt != null &&
                        t.Booking.ExpiresAt > now &&
                        t.Booking.UserId != userId)
                    .Select(t => t.SeatId)
                    .ToListAsync();

                heldSeatIds = holdSeatIds
                    .Union(pendingSeatIds)
                    .ToList();
            }

            ViewBag.MovieId = movie.Id;
            ViewBag.MovieTitle = movie.Title;
            ViewBag.MovieImage = movie.ImageUrl;
            ViewBag.MovieGenres = movie.MovieGenres;
            ViewBag.AgeRating = movie.AgeRating ?? "P";

            ViewBag.WeekDates = weekDates;
            ViewBag.SelectedDate = selectedDay;
            ViewBag.Showtimes = showtimes;
            ViewBag.ShowtimeId = showtime?.Id ?? 0;
            ViewBag.Showtime = showtime;

            ViewBag.Seats = seats;
            ViewBag.SoldSeatIds = soldSeatIds;
            ViewBag.HeldSeatIds = heldSeatIds;
            ViewBag.CurrentUserId = userId;

            return View(new Booking
            {
                BookingDate = DateTime.Now,
                Status = "Pending"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int movieId, int showtimeId, string selectedSeatIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (showtimeId <= 0)
            {
                TempData["Error"] = "Vui lòng chọn suất chiếu trước khi đặt vé.";
                return RedirectToAction(nameof(Create), new { movieId });
            }

            if (string.IsNullOrWhiteSpace(selectedSeatIds))
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một ghế.";
                return RedirectToAction(nameof(Create), new { movieId, showtimeId });
            }

            List<int> seatIds;

            try
            {
                seatIds = selectedSeatIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.Parse(x))
                    .Distinct()
                    .ToList();
            }
            catch
            {
                TempData["Error"] = "Dữ liệu ghế không hợp lệ. Vui lòng chọn lại.";
                return RedirectToAction(nameof(Create), new { movieId, showtimeId });
            }

            if (!seatIds.Any())
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một ghế.";
                return RedirectToAction(nameof(Create), new { movieId, showtimeId });
            }

            if (seatIds.Count > 8)
            {
                TempData["Error"] = "Bạn chỉ được đặt tối đa 8 ghế mỗi lần.";
                return RedirectToAction(nameof(Create), new { movieId, showtimeId });
            }

            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .FirstOrDefaultAsync(s => s.Id == showtimeId);

            if (showtime == null)
            {
                TempData["Error"] = "Không tìm thấy suất chiếu.";
                return RedirectToAction(nameof(Create), new { movieId });
            }

            var now = DateTime.Now;
            string selectedDateText = showtime.StartTime.ToString("yyyy-MM-dd");

            await CleanupExpiredHoldsAsync(now);
            await CleanupExpiredPendingBookingsByShowtimeAsync(showtimeId, now);

            var existingPendingBooking = await _context.Bookings
                .Include(b => b.Tickets)
                .Where(b =>
                    b.UserId == userId &&
                    b.Status == "Pending" &&
                    b.ExpiresAt != null &&
                    b.ExpiresAt > now &&
                    b.Tickets.Any(t => t.ShowtimeId == showtimeId))
                .OrderByDescending(b => b.BookingDate)
                .FirstOrDefaultAsync();

            if (existingPendingBooking != null)
            {
                TempData["Error"] = "Bạn đang có booking chờ thanh toán cho suất chiếu này. Vui lòng tiếp tục thanh toán hoặc hủy giữ ghế.";

                return RedirectToAction(nameof(Checkout), new
                {
                    id = existingPendingBooking.Id
                });
            }

            int validSeatCount = await _context.Seats
                .CountAsync(s => s.RoomId == showtime.RoomId && seatIds.Contains(s.Id));

            if (validSeatCount != seatIds.Count)
            {
                TempData["Error"] = "Có ghế không thuộc phòng chiếu hiện tại. Vui lòng chọn lại.";
                return RedirectToAction(nameof(Create), new
                {
                    movieId,
                    showtimeId,
                    selectedDate = selectedDateText
                });
            }

            var soldSeatIds = await _context.Tickets
                .Where(t =>
                    t.ShowtimeId == showtimeId &&
                    t.Booking != null &&
                    t.Booking.Status == "Completed" &&
                    seatIds.Contains(t.SeatId))
                .Select(t => t.SeatId)
                .ToListAsync();

            if (soldSeatIds.Any())
            {
                TempData["Error"] = "Một số ghế đã được bán. Vui lòng chọn ghế khác.";
                return RedirectToAction(nameof(Create), new
                {
                    movieId,
                    showtimeId,
                    selectedDate = selectedDateText
                });
            }

            var pendingSeatIds = await _context.Tickets
                .Where(t =>
                    t.ShowtimeId == showtimeId &&
                    t.Booking != null &&
                    t.Booking.Status == "Pending" &&
                    t.Booking.ExpiresAt != null &&
                    t.Booking.ExpiresAt > now &&
                    t.Booking.UserId != userId &&
                    seatIds.Contains(t.SeatId))
                .Select(t => t.SeatId)
                .ToListAsync();

            if (pendingSeatIds.Any())
            {
                TempData["Error"] = "Một số ghế đang chờ người khác thanh toán. Vui lòng chọn ghế khác.";
                return RedirectToAction(nameof(Create), new
                {
                    movieId,
                    showtimeId,
                    selectedDate = selectedDateText
                });
            }

            int validHoldCount = await _context.SeatHolds
                .CountAsync(h =>
                    h.ShowtimeId == showtimeId &&
                    seatIds.Contains(h.SeatId) &&
                    h.UserId == userId &&
                    h.ExpiresAt > now);

            if (validHoldCount != seatIds.Count)
            {
                TempData["Error"] = "Một số ghế đã hết thời gian giữ hoặc chưa được giữ hợp lệ. Vui lòng chọn lại.";
                return RedirectToAction(nameof(Create), new
                {
                    movieId,
                    showtimeId,
                    selectedDate = selectedDateText
                });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var totalAmount = seatIds.Count * showtime.BasePrice;

                var booking = new Booking
                {
                    BookingDate = now,
                    ExpiresAt = now.AddMinutes(HoldMinutes),
                    TotalAmount = totalAmount,
                    Status = "Pending",
                    UserId = userId,
                    PayPalTransactionId = null,
                    PayPalOrderId = null,
                    PaidAt = null
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                foreach (var seatId in seatIds)
                {
                    _context.Tickets.Add(new Ticket
                    {
                        BookingId = booking.Id,
                        ShowtimeId = showtimeId,
                        SeatId = seatId,
                        Price = showtime.BasePrice
                    });
                }

                var userHolds = await _context.SeatHolds
                    .Where(h =>
                        h.ShowtimeId == showtimeId &&
                        h.UserId == userId &&
                        seatIds.Contains(h.SeatId))
                    .ToListAsync();

                if (userHolds.Any())
                {
                    _context.SeatHolds.RemoveRange(userHolds);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToAction(nameof(Checkout), new { id = booking.Id });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                TempData["Error"] = "Có ghế vừa được người khác đặt trước. Vui lòng chọn lại.";
                return RedirectToAction(nameof(Create), new
                {
                    movieId,
                    showtimeId,
                    selectedDate = selectedDateText
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IActionResult> Checkout(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var booking = await _context.Bookings
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
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            if (booking.Status == "Completed")
            {
                return RedirectToAction(nameof(Details), new { id = booking.Id });
            }

            if (booking.Status != "Pending")
            {
                TempData["Error"] = "Booking này không còn ở trạng thái chờ thanh toán.";
                return RedirectToAction(nameof(Details), new { id = booking.Id });
            }

            if (IsExpiredPending(booking))
            {
                var expiredInfo = await ExpireBookingAsync(booking);

                TempData["Error"] = "Thời gian giữ ghế đã hết. Vui lòng chọn ghế lại.";

                if (expiredInfo.MovieId.HasValue && expiredInfo.ShowtimeId > 0)
                {
                    return RedirectToAction(nameof(Create), new
                    {
                        movieId = expiredInfo.MovieId.Value,
                        showtimeId = expiredInfo.ShowtimeId
                    });
                }

                return RedirectToAction("Index", "Home");
            }

            ViewBag.PayPalUsdAmount = _payPalService.ConvertVndToUsd(booking.TotalAmount);

            return View(booking);
        }

        public async Task<IActionResult> PayWithPayPal(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var booking = await _context.Bookings
                .Include(b => b.Tickets)
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            if (booking.Status == "Completed")
            {
                TempData["Success"] = "Booking này đã được thanh toán.";
                return RedirectToAction(nameof(Details), new { id = booking.Id });
            }

            if (booking.Status != "Pending")
            {
                TempData["Error"] = "Booking không ở trạng thái chờ thanh toán.";
                return RedirectToAction(nameof(Details), new { id = booking.Id });
            }

            if (IsExpiredPending(booking))
            {
                await ExpireBookingAsync(booking);

                TempData["Error"] = "Thời gian giữ ghế đã hết. Vui lòng chọn ghế lại.";
                return RedirectToAction(nameof(Details), new { id = booking.Id });
            }

            try
            {
                decimal amountUsd = _payPalService.ConvertVndToUsd(booking.TotalAmount);

                string returnUrl = Url.Action(
                    nameof(PayPalSuccess),
                    "Bookings",
                    new { bookingId = booking.Id },
                    Request.Scheme
                ) ?? "";

                string cancelUrl = Url.Action(
                    nameof(PayPalCancel),
                    "Bookings",
                    new { bookingId = booking.Id },
                    Request.Scheme
                ) ?? "";

                string approvalUrl = await _payPalService.CreateOrderAsync(
                    amountUsd,
                    returnUrl,
                    cancelUrl,
                    booking.Id
                );

                return Redirect(approvalUrl);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể tạo thanh toán PayPal: " + ex.Message;
                return RedirectToAction(nameof(Checkout), new { id = booking.Id });
            }
        }

        public async Task<IActionResult> PayPalSuccess(int bookingId, string token)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var booking = await _context.Bookings
                .Include(b => b.Tickets)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            if (booking.Status == "Completed")
            {
                return RedirectToAction(nameof(Details), new { id = booking.Id });
            }

            if (booking.Status != "Pending")
            {
                TempData["Error"] = "Booking này không còn ở trạng thái chờ thanh toán.";
                return RedirectToAction(nameof(Details), new { id = booking.Id });
            }

            if (IsExpiredPending(booking))
            {
                await ExpireBookingAsync(booking);

                TempData["Error"] = "Thời gian giữ ghế đã hết. Thanh toán không còn hợp lệ.";
                return RedirectToAction(nameof(Details), new { id = booking.Id });
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Thiếu mã PayPal order.";
                return RedirectToAction(nameof(Checkout), new { id = booking.Id });
            }

            try
            {
                string captureId = await _payPalService.CaptureOrderAsync(token);

                booking.Status = "Completed";
                booking.PayPalOrderId = token;
                booking.PayPalTransactionId = captureId;
                booking.PaidAt = DateTime.Now;

                await _context.SaveChangesAsync();

                var seatIds = booking.Tickets.Select(t => t.SeatId).ToList();
                var paidShowtimeId = booking.Tickets.FirstOrDefault()?.ShowtimeId ?? 0;

                if (paidShowtimeId > 0 && seatIds.Any())
                {
                    await _ticketHub.Clients
                        .Group($"showtime-{paidShowtimeId}")
                        .SendAsync("SeatSold", new
                        {
                            seatIds
                        });
                }

                TempData["Success"] = "Thanh toán PayPal thành công.";
                return RedirectToAction(nameof(Details), new { id = booking.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Thanh toán PayPal thất bại: " + ex.Message;
                return RedirectToAction(nameof(Checkout), new { id = booking.Id });
            }
        }

        public async Task<IActionResult> PayPalCancel(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            if (booking.Status == "Pending")
            {
                await CancelPendingBookingAsync(booking, "Cancelled");
            }

            TempData["Error"] = "Bạn đã hủy thanh toán PayPal.";
            return RedirectToAction(nameof(Details), new { id = booking.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelPending(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            if (booking.Status != "Pending")
            {
                TempData["Error"] = "Booking này không còn ở trạng thái chờ thanh toán.";
                return RedirectToAction(nameof(Details), new { id = booking.Id });
            }

            var cancelInfo = await CancelPendingBookingAsync(booking, "Cancelled");

            TempData["Success"] = "Đã hủy giữ ghế. Bạn có thể chọn ghế lại.";

            if (cancelInfo.MovieId.HasValue && cancelInfo.ShowtimeId > 0)
            {
                return RedirectToAction(nameof(Create), new
                {
                    movieId = cancelInfo.MovieId.Value,
                    showtimeId = cancelInfo.ShowtimeId
                });
            }

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            if (IsExpiredPending(booking))
            {
                await ExpireBookingAsync(booking);

                TempData["Error"] = "Thời gian giữ ghế đã hết. Thanh toán không còn hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Booking booking)
        {
            if (id != booking.Id)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var existingBooking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (existingBooking == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(booking);
            }

            existingBooking.BookingDate = booking.BookingDate;
            existingBooking.TotalAmount = booking.TotalAmount;
            existingBooking.PayPalTransactionId = booking.PayPalTransactionId;
            existingBooking.Status = booking.Status;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookingExists(existingBooking.Id))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var booking = await _context.Bookings
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
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var booking = await _context.Bookings
                .Include(b => b.Tickets)
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking != null)
            {
                if (booking.Status == "Pending")
                {
                    await CancelPendingBookingAsync(booking, "Cancelled");
                }

                _context.Bookings.Remove(booking);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> QrCode(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound();
            }

            if (!User.IsInRole("Admin") && !User.IsInRole("Staff") && booking.UserId != userId)
            {
                return Forbid();
            }

            if (booking.Status != "Completed")
            {
                return BadRequest("Chỉ vé đã thanh toán thành công mới có mã QR.");
            }

            string token = GenerateTicketToken(booking);

            string verifyUrl = Url.Action(
                action: nameof(VerifyTicket),
                controller: "Bookings",
                values: new { id = booking.Id, token },
                protocol: Request.Scheme
            ) ?? "";

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(verifyUrl, QRCodeGenerator.ECCLevel.Q);

            var qrCode = new PngByteQRCode(qrData);
            byte[] qrBytes = qrCode.GetGraphic(20);

            return File(qrBytes, "image/png");
        }

        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> VerifyTicket(int id, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Mã xác thực không hợp lệ.");
            }

            var booking = await _context.Bookings
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
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound();
            }

            string validToken = GenerateTicketToken(booking);

            bool isValidTicket = token == validToken && booking.Status == "Completed";

            ViewBag.IsValidTicket = isValidTicket;

            return View(booking);
        }

        private bool IsExpiredPending(Booking booking)
        {
            return booking.Status == "Pending" &&
                   booking.ExpiresAt.HasValue &&
                   booking.ExpiresAt.Value <= DateTime.Now;
        }

        private async Task CleanupExpiredHoldsAsync(DateTime now)
        {
            var expiredHolds = await _context.SeatHolds
                .Where(h => h.ExpiresAt <= now)
                .ToListAsync();

            if (expiredHolds.Any())
            {
                _context.SeatHolds.RemoveRange(expiredHolds);
                await _context.SaveChangesAsync();
            }
        }

        private async Task CleanupExpiredPendingBookingsByShowtimeAsync(int showtimeId, DateTime now)
        {
            var expiredBookings = await _context.Bookings
                .Where(b =>
                    b.Status == "Pending" &&
                    b.ExpiresAt != null &&
                    b.ExpiresAt <= now &&
                    b.Tickets.Any(t => t.ShowtimeId == showtimeId))
                .ToListAsync();

            foreach (var booking in expiredBookings)
            {
                await ExpireBookingAsync(booking);
            }
        }

        private async Task<BookingReleaseInfo> ExpireBookingAsync(Booking booking)
        {
            return await ReleasePendingBookingSeatsAsync(booking, "Expired");
        }

        private async Task<BookingReleaseInfo> CancelPendingBookingAsync(Booking booking, string newStatus)
        {
            return await ReleasePendingBookingSeatsAsync(booking, newStatus);
        }

        private async Task<BookingReleaseInfo> ReleasePendingBookingSeatsAsync(Booking booking, string newStatus)
        {
            var ticketInfos = await _context.Tickets
                .Where(t => t.BookingId == booking.Id)
                .Select(t => new
                {
                    t.Id,
                    t.SeatId,
                    t.ShowtimeId,
                    MovieId = (int?)t.Showtime.MovieId
                })
                .ToListAsync();

            int showtimeId = ticketInfos.FirstOrDefault()?.ShowtimeId ?? 0;
            int? movieId = ticketInfos.FirstOrDefault()?.MovieId;
            var seatIds = ticketInfos.Select(t => t.SeatId).ToList();

            var tickets = await _context.Tickets
                .Where(t => t.BookingId == booking.Id)
                .ToListAsync();

            if (tickets.Any())
            {
                _context.Tickets.RemoveRange(tickets);
            }

            if (showtimeId > 0 && seatIds.Any())
            {
                var relatedHolds = await _context.SeatHolds
                    .Where(h =>
                        h.ShowtimeId == showtimeId &&
                        seatIds.Contains(h.SeatId))
                    .ToListAsync();

                if (relatedHolds.Any())
                {
                    _context.SeatHolds.RemoveRange(relatedHolds);
                }
            }

            booking.Status = newStatus;
            booking.ExpiresAt = DateTime.Now;

            if (newStatus != "Completed")
            {
                booking.PayPalOrderId = null;
                booking.PayPalTransactionId = null;
                booking.PaidAt = null;
            }

            await _context.SaveChangesAsync();

            if (showtimeId > 0 && seatIds.Any())
            {
                await _ticketHub.Clients
                    .Group($"showtime-{showtimeId}")
                    .SendAsync("SeatReleasedBatch", new
                    {
                        seatIds
                    });
            }

            return new BookingReleaseInfo
            {
                ShowtimeId = showtimeId,
                MovieId = movieId,
                SeatIds = seatIds
            };
        }

        private static string GenerateTicketToken(Booking booking)
        {
            const string secretKey = "CINEZONE_SECRET_QR_KEY_2026_CHANGE_ME";

            string rawData =
                $"{booking.Id}|{booking.UserId}|{booking.BookingDate.Ticks}|{booking.TotalAmount}|{booking.Status}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return Convert.ToBase64String(hashBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private bool BookingExists(int id)
        {
            return _context.Bookings.Any(e => e.Id == id);
        }

        private class BookingReleaseInfo
        {
            public int ShowtimeId { get; set; }

            public int? MovieId { get; set; }

            public List<int> SeatIds { get; set; } = new();
        }
    }
}