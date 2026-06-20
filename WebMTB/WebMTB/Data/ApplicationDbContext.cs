using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebMTB.Models;

namespace WebMTB.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // --- NHÓM PHIM & NỘI DUNG ---
        public DbSet<Movie> Movies { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<MovieGenre> MovieGenres { get; set; }
        public DbSet<Actor> Actors { get; set; }
        public DbSet<MovieActor> MovieActors { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Favorite> Favorites { get; set; }

        // --- NHÓM RẠP & SUẤT CHIẾU ---
        public DbSet<CinemaChain> CinemaChains { get; set; }
        public DbSet<Cinema> Cinemas { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<SeatType> SeatTypes { get; set; }
        public DbSet<Seat> Seats { get; set; }
        public DbSet<Showtime> Showtimes { get; set; }
        public DbSet<PriceConfig> PriceConfigs { get; set; }

        // --- NHÓM ĐẶT VÉ & THANH TOÁN ---
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<SeatHold> SeatHolds { get; set; }
        public DbSet<Combo> Combos { get; set; }
        public DbSet<BookingCombo> BookingCombos { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Khai báo khóa chính kép cho các bảng trung gian
            modelBuilder.Entity<MovieGenre>()
                .HasKey(mg => new { mg.MovieId, mg.GenreId });

            modelBuilder.Entity<MovieActor>()
                .HasKey(ma => new { ma.MovieId, ma.ActorId });

            // 2. Cấu hình độ chính xác cho tiền tệ (Precision)
            modelBuilder.Entity<Booking>().Property(b => b.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Ticket>().Property(t => t.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Showtime>().Property(s => s.BasePrice).HasPrecision(18, 2);
            modelBuilder.Entity<SeatType>().Property(st => st.ExtraPrice).HasPrecision(18, 2);
            modelBuilder.Entity<PriceConfig>().Property(pc => pc.Surcharge).HasPrecision(18, 2);
            modelBuilder.Entity<Combo>().Property(c => c.Price).HasPrecision(18, 2);

            // 3. FIX LỖI CASCADE DELETE (Dứt điểm lỗi Cycles/Multiple cascade paths)

            // Chặn xóa dây chuyền từ Showtime -> Ticket
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Showtime)
                .WithMany()
                .HasForeignKey(t => t.ShowtimeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Chặn xóa dây chuyền từ Booking -> Ticket
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Booking)
                .WithMany(b => b.Tickets)
                .HasForeignKey(t => t.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            // Chặn xóa dây chuyền từ Seat -> Ticket
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Seat)
                .WithMany()
                .HasForeignKey(t => t.SeatId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. Cấu hình SeatHold - giữ ghế tạm thời
            modelBuilder.Entity<SeatHold>()
                .HasIndex(sh => new { sh.ShowtimeId, sh.SeatId })
                .IsUnique();

            modelBuilder.Entity<SeatHold>()
                .HasOne(sh => sh.Showtime)
                .WithMany()
                .HasForeignKey(sh => sh.ShowtimeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SeatHold>()
                .HasOne(sh => sh.Seat)
                .WithMany()
                .HasForeignKey(sh => sh.SeatId)
                .OnDelete(DeleteBehavior.Restrict);

            // 5. Chặn bán trùng ghế trong cùng một suất chiếu
            modelBuilder.Entity<Ticket>()
                .HasIndex(t => new { t.ShowtimeId, t.SeatId })
                .IsUnique();
        }
    }
}