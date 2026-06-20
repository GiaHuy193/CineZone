namespace WebMTB.Models
{
    public class SeatType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // Ví dụ: VIP
        public decimal ExtraPrice { get; set; } // Số tiền cộng thêm so với giá gốc
    }
}
