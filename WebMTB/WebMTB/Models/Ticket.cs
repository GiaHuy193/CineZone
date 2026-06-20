namespace WebMTB.Models
{
    public class Ticket
    {
        public int Id { get; set; }
        public decimal Price { get; set; } // Giá tại thời điểm mua
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public int ShowtimeId { get; set; }
        public Showtime? Showtime { get; set; }
        public int SeatId { get; set; }
        public Seat? Seat { get; set; }
    }
}
