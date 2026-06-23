using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebMTB.Data;
using WebMTB.Models;

namespace WebMTB.Hubs
{
    public class TicketHub : Hub
    {
        private readonly ApplicationDbContext _context;

        private const int HoldMinutes = 4;
        private const int MaxSeatsPerBooking = 8;

        public TicketHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task JoinShowtimeRoom(int showtimeId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"showtime-{showtimeId}");
        }

        public async Task<object> HoldSeat(int showtimeId, int seatId)
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return new
                {
                    success = false,
                    message = "Bạn cần đăng nhập để giữ ghế."
                };
            }

            var now = DateTime.Now;

            // 1. Dọn ghế giữ hết hạn và báo realtime cho các client khác
            await CleanupExpiredHoldsAsync(now);

            // 2. Kiểm tra suất chiếu
            var showtime = await _context.Showtimes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == showtimeId);

            if (showtime == null)
            {
                return new
                {
                    success = false,
                    message = "Không tìm thấy suất chiếu."
                };
            }

            // 3. Kiểm tra ghế có thuộc đúng phòng chiếu của suất này không
            bool seatBelongsToRoom = await _context.Seats
                .AnyAsync(s => s.Id == seatId && s.RoomId == showtime.RoomId);

            if (!seatBelongsToRoom)
            {
                return new
                {
                    success = false,
                    message = "Ghế không thuộc phòng chiếu hiện tại."
                };
            }

            // 4. Kiểm tra ghế đã bán chưa
            bool isSold = await _context.Tickets
                .AnyAsync(t =>
                    t.ShowtimeId == showtimeId &&
                    t.SeatId == seatId &&
                    t.Booking != null &&
                    t.Booking.Status == "Completed");

            if (isSold)
            {
                return new
                {
                    success = false,
                    message = "Ghế này đã được bán."
                };
            }

            // 5. Kiểm tra ghế đang nằm trong booking pending của người khác chưa
            bool isInPendingBooking = await _context.Tickets
                .AnyAsync(t =>
                    t.ShowtimeId == showtimeId &&
                    t.SeatId == seatId &&
                    t.Booking != null &&
                    t.Booking.Status == "Pending" &&
                    t.Booking.ExpiresAt != null &&
                    t.Booking.ExpiresAt > now &&
                    t.Booking.UserId != userId);

            if (isInPendingBooking)
            {
                return new
                {
                    success = false,
                    message = "Ghế này đang chờ người khác thanh toán."
                };
            }

            // 6. Kiểm tra ghế đang bị giữ chưa
            var existingHold = await _context.SeatHolds
                .FirstOrDefaultAsync(h =>
                    h.ShowtimeId == showtimeId &&
                    h.SeatId == seatId &&
                    h.ExpiresAt > now);

            if (existingHold != null)
            {
                // Nếu chính user này đã giữ ghế đó rồi thì trả success, không báo lỗi
                if (existingHold.UserId == userId)
                {
                    return new
                    {
                        success = true,
                        message = "Bạn đang giữ ghế này.",
                        expiresAt = existingHold.ExpiresAt
                    };
                }

                return new
                {
                    success = false,
                    message = "Ghế này đang được giữ. Vui lòng chọn ghế khác."
                };
            }

            // 7. Chặn user giữ quá 8 ghế trong cùng suất chiếu
            int currentUserHoldCount = await _context.SeatHolds
                .CountAsync(h =>
                    h.ShowtimeId == showtimeId &&
                    h.UserId == userId &&
                    h.ExpiresAt > now);

            if (currentUserHoldCount >= MaxSeatsPerBooking)
            {
                return new
                {
                    success = false,
                    message = "Bạn chỉ được giữ tối đa 8 ghế trong một lần đặt. Nếu muốn đặt vé nhóm, vui lòng liên hệ rạp."
                };
            }

            // 8. Tạo giữ ghế mới
            var hold = new SeatHold
            {
                ShowtimeId = showtimeId,
                SeatId = seatId,
                UserId = userId,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(HoldMinutes)
            };

            _context.SeatHolds.Add(hold);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return new
                {
                    success = false,
                    message = "Ghế này vừa được người khác giữ. Vui lòng chọn ghế khác."
                };
            }

            // 9. Báo cho các client khác trong cùng suất chiếu
            await Clients.OthersInGroup($"showtime-{showtimeId}")
                .SendAsync("SeatHeld", seatId, userId);

            return new
            {
                success = true,
                message = "Giữ ghế thành công.",
                expiresAt = hold.ExpiresAt
            };
        }

        public async Task ReleaseSeat(int showtimeId, int seatId)
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var hold = await _context.SeatHolds
                .FirstOrDefaultAsync(h =>
                    h.ShowtimeId == showtimeId &&
                    h.SeatId == seatId &&
                    h.UserId == userId);

            if (hold == null)
            {
                return;
            }

            _context.SeatHolds.Remove(hold);
            await _context.SaveChangesAsync();

            await Clients.OthersInGroup($"showtime-{showtimeId}")
                .SendAsync("SeatReleased", seatId, userId);
        }

        public async Task ReleaseMySeats(int showtimeId)
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var holds = await _context.SeatHolds
                .Where(h =>
                    h.ShowtimeId == showtimeId &&
                    h.UserId == userId)
                .ToListAsync();

            if (!holds.Any())
            {
                return;
            }

            var seatIds = holds.Select(h => h.SeatId).ToList();

            _context.SeatHolds.RemoveRange(holds);
            await _context.SaveChangesAsync();

            await Clients.OthersInGroup($"showtime-{showtimeId}")
                .SendAsync("SeatReleasedBatch", new
                {
                    seatIds
                });
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Không tự release toàn bộ ghế ở đây.
            // Vì khi user chuyển sang Checkout, SignalR có thể disconnect tạm thời.
            // Nếu release ở đây sẽ làm mất ghế đang chờ thanh toán.
            await base.OnDisconnectedAsync(exception);
        }

        private string? GetCurrentUserId()
        {
            return Context.UserIdentifier
                ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        private async Task CleanupExpiredHoldsAsync(DateTime now)
        {
            var expiredHolds = await _context.SeatHolds
                .Where(h => h.ExpiresAt <= now)
                .ToListAsync();

            if (!expiredHolds.Any())
            {
                return;
            }

            var releasedByShowtime = expiredHolds
                .GroupBy(h => h.ShowtimeId)
                .Select(g => new
                {
                    ShowtimeId = g.Key,
                    SeatIds = g.Select(x => x.SeatId).Distinct().ToList()
                })
                .ToList();

            _context.SeatHolds.RemoveRange(expiredHolds);
            await _context.SaveChangesAsync();

            foreach (var item in releasedByShowtime)
            {
                await Clients.Group($"showtime-{item.ShowtimeId}")
                    .SendAsync("SeatReleasedBatch", new
                    {
                        seatIds = item.SeatIds
                    });
            }
        }
    }
}