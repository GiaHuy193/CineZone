namespace WebMTB.Models
{
    public class Showtime
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public decimal BasePrice { get; set; } // Giá gốc
        public bool IsHoliday { get; set; } // Đánh dấu ngày lễ để cộng tiền

        public int MovieId { get; set; }
        public Movie? Movie { get; set; }
        public int RoomId { get; set; }
        public Room? Room { get; set; }
    }
}
