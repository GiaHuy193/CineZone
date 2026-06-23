using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Hubs;

namespace WebMTB.Service
{
    public class ExpiredBookingCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ExpiredBookingCleanupService> _logger;

        public ExpiredBookingCleanupService(
            IServiceProvider serviceProvider,
            ILogger<ExpiredBookingCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();

                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var ticketHub = scope.ServiceProvider.GetRequiredService<IHubContext<TicketHub>>();

                    var now = DateTime.Now;

                    await CleanupExpiredPendingBookingsAsync(context, ticketHub, now, stoppingToken);
                    await CleanupExpiredSeatHoldsAsync(context, ticketHub, now, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // App đang stop, không cần log lỗi.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi dọn booking/ghế giữ hết hạn.");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private static async Task CleanupExpiredPendingBookingsAsync(
            ApplicationDbContext context,
            IHubContext<TicketHub> ticketHub,
            DateTime now,
            CancellationToken stoppingToken)
        {
            var expiredBookings = await context.Bookings
                .Include(b => b.Tickets)
                .Where(b =>
                    b.Status == "Pending" &&
                    b.ExpiresAt != null &&
                    b.ExpiresAt <= now)
                .ToListAsync(stoppingToken);

            if (!expiredBookings.Any())
            {
                return;
            }

            var releasedGroups = new List<ReleasedSeatGroup>();

            foreach (var booking in expiredBookings)
            {
                var showtimeId = booking.Tickets.FirstOrDefault()?.ShowtimeId ?? 0;
                var seatIds = booking.Tickets.Select(t => t.SeatId).Distinct().ToList();

                if (showtimeId > 0 && seatIds.Any())
                {
                    releasedGroups.Add(new ReleasedSeatGroup
                    {
                        ShowtimeId = showtimeId,
                        SeatIds = seatIds
                    });
                }

                if (booking.Tickets.Any())
                {
                    context.Tickets.RemoveRange(booking.Tickets);
                }

                booking.Status = "Expired";
                booking.ExpiresAt = now;
                booking.PayPalOrderId = null;
                booking.PayPalTransactionId = null;
                booking.PaidAt = null;
            }

            await context.SaveChangesAsync(stoppingToken);

            foreach (var group in releasedGroups)
            {
                await ticketHub.Clients
                    .Group($"showtime-{group.ShowtimeId}")
                    .SendAsync("SeatReleasedBatch", new
                    {
                        seatIds = group.SeatIds
                    }, stoppingToken);
            }
        }

        private static async Task CleanupExpiredSeatHoldsAsync(
            ApplicationDbContext context,
            IHubContext<TicketHub> ticketHub,
            DateTime now,
            CancellationToken stoppingToken)
        {
            var expiredHolds = await context.SeatHolds
                .Where(h => h.ExpiresAt <= now)
                .ToListAsync(stoppingToken);

            if (!expiredHolds.Any())
            {
                return;
            }

            var releasedGroups = expiredHolds
                .GroupBy(h => h.ShowtimeId)
                .Select(g => new ReleasedSeatGroup
                {
                    ShowtimeId = g.Key,
                    SeatIds = g.Select(x => x.SeatId).Distinct().ToList()
                })
                .ToList();

            context.SeatHolds.RemoveRange(expiredHolds);
            await context.SaveChangesAsync(stoppingToken);

            foreach (var group in releasedGroups)
            {
                await ticketHub.Clients
                    .Group($"showtime-{group.ShowtimeId}")
                    .SendAsync("SeatReleasedBatch", new
                    {
                        seatIds = group.SeatIds
                    }, stoppingToken);
            }
        }

        private class ReleasedSeatGroup
        {
            public int ShowtimeId { get; set; }

            public List<int> SeatIds { get; set; } = new();
        }
    }
}