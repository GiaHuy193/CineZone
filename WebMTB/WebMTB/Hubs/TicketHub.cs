using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Models;

namespace WebMTB.Hubs
{
    public class TicketHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public TicketHub(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task JoinShowtimeRoom(int showtimeId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"showtime-{showtimeId}");
        }

        public async Task<object> HoldSeat(int showtimeId, int seatId)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                return new
                {
                    success = false,
                    message = "Bạn cần đăng nhập để giữ ghế."
                };
            }

            var now = DateTime.Now;

            // 1. Xóa các ghế giữ đã hết hạn
            var expiredHolds = await _context.SeatHolds
                .Where(h => h.ExpiresAt <= now)
                .ToListAsync();

            if (expiredHolds.Any())
            {
                _context.SeatHolds.RemoveRange(expiredHolds);
                await _context.SaveChangesAsync();
            }

            // 2. Kiểm tra ghế đã bán chưa
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

            // 3. Kiểm tra ghế đang bị giữ chưa
            var existingHold = await _context.SeatHolds
                .FirstOrDefaultAsync(h =>
                    h.ShowtimeId == showtimeId &&
                    h.SeatId == seatId &&
                    h.ExpiresAt > now);

            // Bản strict: ghế đã được giữ thì thiết bị khác / user khác đều không được chọn
            if (existingHold != null)
            {
                return new
                {
                    success = false,
                    message = "Ghế này đang được giữ. Vui lòng chọn ghế khác."
                };
            }

            // 4. Tạo giữ ghế mới
            var hold = new SeatHold
            {
                ShowtimeId = showtimeId,
                SeatId = seatId,
                UserId = userId,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(5)
            };

            _context.SeatHolds.Add(hold);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                return new
                {
                    success = false,
                    message = "Ghế này vừa được người khác giữ."
                };
            }

            // 5. Báo cho các client khác trong cùng suất chiếu
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
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            var hold = await _context.SeatHolds
                .FirstOrDefaultAsync(h =>
                    h.ShowtimeId == showtimeId &&
                    h.SeatId == seatId &&
                    h.UserId == userId);

            if (hold != null)
            {
                _context.SeatHolds.Remove(hold);
                await _context.SaveChangesAsync();

                await Clients.OthersInGroup($"showtime-{showtimeId}")
                    .SendAsync("SeatReleased", seatId, userId);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}