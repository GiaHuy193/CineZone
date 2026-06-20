using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace WebMTB.Models
{
    public class SeatHold
    {
        public int Id { get; set; }

        public int ShowtimeId { get; set; }
        public virtual Showtime? Showtime { get; set; }

        public int SeatId { get; set; }
        public virtual Seat? Seat { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public virtual IdentityUser? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime ExpiresAt { get; set; }
    }
}