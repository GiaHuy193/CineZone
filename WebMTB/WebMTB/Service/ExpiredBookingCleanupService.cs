using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Hubs;

namespace WebMTB.Service
{
    public class ExpiredBookingCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public ExpiredBookingCleanupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();

                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var ticketHub = scope.ServiceProvider.GetRequiredService<IHubContext<TicketHub>>();

                var now = DateTime.Now;

                var expiredBookings = await context.Bookings
                    .Include(b => b.Tickets)
                    .Where(b =>
                        b.Status == "Pending" &&
                        b.ExpiresAt != null &&
                        b.ExpiresAt <= now)
                    .ToListAsync(stoppingToken);

                foreach (var booking in expiredBookings)
                {
                    var showtimeId = booking.Tickets.FirstOrDefault()?.ShowtimeId ?? 0;
                    var seatIds = booking.Tickets.Select(t => t.SeatId).ToList();

                    if (booking.Tickets.Any())
                    {
                        context.Tickets.RemoveRange(booking.Tickets);
                    }

                    booking.Status = "Expired";

                    if (showtimeId > 0 && seatIds.Any())
                    {
                        await ticketHub.Clients
                            .Group($"showtime-{showtimeId}")
                            .SendAsync("SeatReleasedBatch", new
                            {
                                seatIds = seatIds
                            }, stoppingToken);
                    }
                }

                var expiredHolds = await context.SeatHolds
                    .Where(h => h.ExpiresAt <= now)
                    .ToListAsync(stoppingToken);

                if (expiredHolds.Any())
                {
                    context.SeatHolds.RemoveRange(expiredHolds);
                }

                await context.SaveChangesAsync(stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}