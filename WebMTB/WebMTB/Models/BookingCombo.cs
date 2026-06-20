namespace WebMTB.Models
{
    public class BookingCombo
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public int ComboId { get; set; }
        public Combo? Combo { get; set; }
        public int Quantity { get; set; }
    }
}
