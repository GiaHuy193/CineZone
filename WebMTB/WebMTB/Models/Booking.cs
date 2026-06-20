using Microsoft.AspNetCore.Identity;

namespace WebMTB.Models
{
    public class Booking
    {
        public int Id { get; set; }

        public DateTime BookingDate { get; set; } = DateTime.Now;
        public DateTime? ExpiresAt { get; set; }

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Pending";
        // Pending, Completed, Cancelled, Expired

        public string? PayPalOrderId { get; set; }

        public string? PayPalTransactionId { get; set; }

        public DateTime? PaidAt { get; set; }

        public string UserId { get; set; } = string.Empty;

        public virtual IdentityUser? User { get; set; }

        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

        public virtual ICollection<BookingCombo> BookingCombos { get; set; } = new List<BookingCombo>();
    }
}