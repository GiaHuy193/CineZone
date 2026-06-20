using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;

namespace WebMTB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int? cinemaId)
        {
            var from = fromDate?.Date ?? DateTime.Today.AddDays(-30);
            var to = toDate?.Date.AddDays(1) ?? DateTime.Today.AddDays(1);

            if (from >= to)
            {
                from = DateTime.Today.AddDays(-30);
                to = DateTime.Today.AddDays(1);
            }

            // ===============================
            // 0. Dữ liệu filter giao diện
            // ===============================
            ViewBag.FromDate = from.ToString("yyyy-MM-dd");
            ViewBag.ToDate = to.AddDays(-1).ToString("yyyy-MM-dd");

            ViewBag.FromDateDisplay = from.ToString("dd/MM/yyyy");
            ViewBag.ToDateDisplay = to.AddDays(-1).ToString("dd/MM/yyyy");
            ViewBag.ReportRangeText = $"{from:dd/MM/yyyy} - {to.AddDays(-1):dd/MM/yyyy}";

            ViewBag.Cinemas = await _context.Cinemas
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.SelectedCinemaId = cinemaId;

            ViewBag.SelectedCinemaName = cinemaId.HasValue
                ? await _context.Cinemas
                    .Where(c => c.Id == cinemaId.Value)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync()
                : "Tất cả rạp";

            if (string.IsNullOrWhiteSpace(ViewBag.SelectedCinemaName))
            {
                ViewBag.SelectedCinemaName = "Tất cả rạp";
                cinemaId = null;
                ViewBag.SelectedCinemaId = null;
            }

            // ===============================
            // 1. Thống kê tổng quan hệ thống
            // ===============================
            ViewBag.TotalMovies = await _context.Movies.CountAsync();
            ViewBag.TotalCinemas = await _context.Cinemas.CountAsync();
            ViewBag.TotalRooms = cinemaId.HasValue
                ? await _context.Rooms.CountAsync(r => r.CinemaId == cinemaId.Value)
                : await _context.Rooms.CountAsync();

            ViewBag.TotalSeats = cinemaId.HasValue
                ? await _context.Seats
                    .Where(s => s.Room != null && s.Room.CinemaId == cinemaId.Value)
                    .CountAsync()
                : await _context.Seats.CountAsync();

            ViewBag.TotalShowtimes = cinemaId.HasValue
                ? await _context.Showtimes
                    .Where(s => s.Room != null && s.Room.CinemaId == cinemaId.Value)
                    .CountAsync()
                : await _context.Showtimes.CountAsync();

            // ===============================
            // 2. Query booking theo ngày + rạp
            // ===============================
            var filteredBookingQuery = _context.Bookings
                .Where(b =>
                    b.BookingDate >= from &&
                    b.BookingDate < to);

            if (cinemaId.HasValue)
            {
                filteredBookingQuery = filteredBookingQuery.Where(b =>
                    b.Tickets.Any(t =>
                        t.Showtime != null &&
                        t.Showtime.Room != null &&
                        t.Showtime.Room.CinemaId == cinemaId.Value));
            }

            ViewBag.TotalBookings = await filteredBookingQuery.CountAsync();

            int pendingBookings = await filteredBookingQuery
                .CountAsync(b => b.Status == "Pending");

            int completedBookings = await filteredBookingQuery
                .CountAsync(b => b.Status == "Completed");

            int cancelledBookings = await filteredBookingQuery
                .CountAsync(b => b.Status == "Cancelled");

            ViewBag.PendingBookings = pendingBookings;
            ViewBag.CompletedBookings = completedBookings;
            ViewBag.CancelledBookings = cancelledBookings;

            ViewBag.TotalRevenue = await filteredBookingQuery
                .Where(b => b.Status == "Completed")
                .SumAsync(b => (decimal?)b.TotalAmount) ?? 0;

            // ===============================
            // 3. Doanh thu hôm nay theo rạp đang chọn
            // ===============================
            var todayStart = DateTime.Today;
            var tomorrowStart = DateTime.Today.AddDays(1);

            var todayRevenueQuery = _context.Bookings
                .Where(b =>
                    b.Status == "Completed" &&
                    b.BookingDate >= todayStart &&
                    b.BookingDate < tomorrowStart);

            if (cinemaId.HasValue)
            {
                todayRevenueQuery = todayRevenueQuery.Where(b =>
                    b.Tickets.Any(t =>
                        t.Showtime != null &&
                        t.Showtime.Room != null &&
                        t.Showtime.Room.CinemaId == cinemaId.Value));
            }

            ViewBag.TodayRevenue = await todayRevenueQuery
                .SumAsync(b => (decimal?)b.TotalAmount) ?? 0;

            // ===============================
            // 4. Biểu đồ doanh thu 7 ngày gần nhất
            // Có lọc theo rạp nếu admin chọn rạp
            // ===============================
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-6 + i))
                .ToList();

            var revenueLast7Days = new List<decimal>();
            var bookingLast7Days = new List<int>();

            foreach (var day in last7Days)
            {
                var nextDay = day.AddDays(1);

                var dayBookingQuery = _context.Bookings
                    .Where(b =>
                        b.BookingDate >= day &&
                        b.BookingDate < nextDay);

                if (cinemaId.HasValue)
                {
                    dayBookingQuery = dayBookingQuery.Where(b =>
                        b.Tickets.Any(t =>
                            t.Showtime != null &&
                            t.Showtime.Room != null &&
                            t.Showtime.Room.CinemaId == cinemaId.Value));
                }

                decimal dayRevenue = await dayBookingQuery
                    .Where(b => b.Status == "Completed")
                    .SumAsync(b => (decimal?)b.TotalAmount) ?? 0;

                int dayBookingCount = await dayBookingQuery.CountAsync();

                revenueLast7Days.Add(dayRevenue);
                bookingLast7Days.Add(dayBookingCount);
            }

            ViewBag.ChartLabels = last7Days
                .Select(d => d.ToString("dd/MM"))
                .ToList();

            ViewBag.RevenueLast7Days = revenueLast7Days;
            ViewBag.BookingLast7Days = bookingLast7Days;

            // ===============================
            // 5. Biểu đồ trạng thái booking
            // Theo ngày + rạp đang chọn
            // ===============================
            ViewBag.StatusLabels = new List<string>
            {
                "Pending",
                "Completed",
                "Cancelled"
            };

            ViewBag.StatusData = new List<int>
            {
                pendingBookings,
                completedBookings,
                cancelledBookings
            };

            // ===============================
            // 6. Booking gần đây
            // Theo rạp đang chọn
            // ===============================
            var recentBookingQuery = _context.Bookings
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

            if (cinemaId.HasValue)
            {
                recentBookingQuery = recentBookingQuery.Where(b =>
                    b.Tickets.Any(t =>
                        t.Showtime != null &&
                        t.Showtime.Room != null &&
                        t.Showtime.Room.CinemaId == cinemaId.Value));
            }

            var recentBookings = await recentBookingQuery
                .OrderByDescending(b => b.BookingDate)
                .Take(8)
                .ToListAsync();

            return View(recentBookings);
        }

        public async Task<IActionResult> ExportRevenueExcel(DateTime? fromDate, DateTime? toDate, int? cinemaId)
        {
            var from = fromDate?.Date ?? DateTime.Today.AddDays(-30);
            var to = toDate?.Date.AddDays(1) ?? DateTime.Today.AddDays(1);

            if (from >= to)
            {
                from = DateTime.Today.AddDays(-30);
                to = DateTime.Today.AddDays(1);
            }

            var selectedCinemaName = cinemaId.HasValue
                ? await _context.Cinemas
                    .Where(c => c.Id == cinemaId.Value)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync()
                : "Tất cả rạp";

            if (string.IsNullOrWhiteSpace(selectedCinemaName))
            {
                selectedCinemaName = "Tất cả rạp";
                cinemaId = null;
            }

            var bookingQuery = _context.Bookings
                .Include(b => b.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(b => b.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Room)
                            .ThenInclude(r => r.Cinema)
                .Where(b =>
                    b.Status == "Completed" &&
                    b.BookingDate >= from &&
                    b.BookingDate < to);

            if (cinemaId.HasValue)
            {
                bookingQuery = bookingQuery.Where(b =>
                    b.Tickets.Any(t =>
                        t.Showtime != null &&
                        t.Showtime.Room != null &&
                        t.Showtime.Room.CinemaId == cinemaId.Value));
            }

            var bookings = await bookingQuery
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            var bookingRows = bookings.Select(b =>
            {
                var firstTicket = b.Tickets.FirstOrDefault();
                var showtime = firstTicket?.Showtime;
                var movie = showtime?.Movie;
                var room = showtime?.Room;
                var cinema = room?.Cinema;

                return new
                {
                    BookingId = b.Id,
                    BookingDate = b.BookingDate,
                    CinemaName = cinema?.Name ?? "Không rõ rạp",
                    RoomName = room?.Name ?? "Không rõ phòng",
                    MovieTitle = movie?.Title ?? "Không rõ phim",
                    Showtime = showtime?.StartTime,
                    TicketCount = b.Tickets.Count,
                    TotalAmount = b.TotalAmount,
                    Status = b.Status,
                    PayPalTransactionId = b.PayPalTransactionId
                };
            }).ToList();

            using var workbook = new XLWorkbook();

            // ===============================
            // Sheet 1: Tổng hợp theo rạp
            // ===============================
            var summarySheet = workbook.Worksheets.Add("Tong hop theo rap");

            summarySheet.Cell(1, 1).Value = $"BÁO CÁO DOANH THU - {selectedCinemaName}";
            summarySheet.Range(1, 1, 1, 6).Merge();

            summarySheet.Cell(1, 1).Style.Font.Bold = true;
            summarySheet.Cell(1, 1).Style.Font.FontSize = 16;
            summarySheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            summarySheet.Cell(2, 1).Value = "Từ ngày";
            summarySheet.Cell(2, 2).Value = from.ToString("dd/MM/yyyy");

            summarySheet.Cell(2, 4).Value = "Đến ngày";
            summarySheet.Cell(2, 5).Value = to.AddDays(-1).ToString("dd/MM/yyyy");

            summarySheet.Cell(3, 1).Value = "Rạp áp dụng";
            summarySheet.Cell(3, 2).Value = selectedCinemaName;

            summarySheet.Cell(5, 1).Value = "STT";
            summarySheet.Cell(5, 2).Value = "Rạp";
            summarySheet.Cell(5, 3).Value = "Số booking";
            summarySheet.Cell(5, 4).Value = "Số vé";
            summarySheet.Cell(5, 5).Value = "Doanh thu";
            summarySheet.Cell(5, 6).Value = "Tỉ lệ doanh thu";

            var totalRevenue = bookingRows.Sum(x => x.TotalAmount);

            var revenueByCinema = bookingRows
                .GroupBy(x => x.CinemaName)
                .Select(g => new
                {
                    CinemaName = g.Key,
                    BookingCount = g.Count(),
                    TicketCount = g.Sum(x => x.TicketCount),
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            int row = 6;
            int index = 1;

            foreach (var item in revenueByCinema)
            {
                summarySheet.Cell(row, 1).Value = index++;
                summarySheet.Cell(row, 2).Value = item.CinemaName;
                summarySheet.Cell(row, 3).Value = item.BookingCount;
                summarySheet.Cell(row, 4).Value = item.TicketCount;
                summarySheet.Cell(row, 5).Value = item.Revenue;
                summarySheet.Cell(row, 6).Value = totalRevenue > 0
                    ? item.Revenue / totalRevenue
                    : 0;

                row++;
            }

            summarySheet.Cell(row + 1, 4).Value = "Tổng doanh thu";
            summarySheet.Cell(row + 1, 5).Value = totalRevenue;

            summarySheet.Range(5, 1, 5, 6).Style.Font.Bold = true;
            summarySheet.Range(5, 1, 5, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#E50914");
            summarySheet.Range(5, 1, 5, 6).Style.Font.FontColor = XLColor.White;

            summarySheet.Column(5).Style.NumberFormat.Format = "#,##0";
            summarySheet.Column(6).Style.NumberFormat.Format = "0.00%";
            summarySheet.Columns().AdjustToContents();

            // ===============================
            // Sheet 2: Chi tiết booking
            // ===============================
            var detailSheet = workbook.Worksheets.Add("Chi tiet booking");

            detailSheet.Cell(1, 1).Value = "Mã booking";
            detailSheet.Cell(1, 2).Value = "Ngày đặt";
            detailSheet.Cell(1, 3).Value = "Rạp";
            detailSheet.Cell(1, 4).Value = "Phòng";
            detailSheet.Cell(1, 5).Value = "Phim";
            detailSheet.Cell(1, 6).Value = "Suất chiếu";
            detailSheet.Cell(1, 7).Value = "Số vé";
            detailSheet.Cell(1, 8).Value = "Doanh thu";
            detailSheet.Cell(1, 9).Value = "Trạng thái";
            detailSheet.Cell(1, 10).Value = "Mã giao dịch PayPal";

            int detailRow = 2;

            foreach (var item in bookingRows)
            {
                detailSheet.Cell(detailRow, 1).Value = item.BookingId;
                detailSheet.Cell(detailRow, 2).Value = item.BookingDate.ToString("dd/MM/yyyy HH:mm");
                detailSheet.Cell(detailRow, 3).Value = item.CinemaName;
                detailSheet.Cell(detailRow, 4).Value = item.RoomName;
                detailSheet.Cell(detailRow, 5).Value = item.MovieTitle;
                detailSheet.Cell(detailRow, 6).Value = item.Showtime?.ToString("dd/MM/yyyy HH:mm") ?? "";
                detailSheet.Cell(detailRow, 7).Value = item.TicketCount;
                detailSheet.Cell(detailRow, 8).Value = item.TotalAmount;
                detailSheet.Cell(detailRow, 9).Value = item.Status;
                detailSheet.Cell(detailRow, 10).Value = item.PayPalTransactionId ?? "";

                detailRow++;
            }

            detailSheet.Range(1, 1, 1, 10).Style.Font.Bold = true;
            detailSheet.Range(1, 1, 1, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#E50914");
            detailSheet.Range(1, 1, 1, 10).Style.Font.FontColor = XLColor.White;
            detailSheet.Column(8).Style.NumberFormat.Format = "#,##0";
            detailSheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var safeCinemaName = selectedCinemaName
                .Replace(" ", "_")
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace(":", "_");

            var fileName = $"BaoCaoDoanhThu_{safeCinemaName}_{from:yyyyMMdd}_{to.AddDays(-1):yyyyMMdd}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}