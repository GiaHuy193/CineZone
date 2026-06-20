namespace WebMTB.Models
{
    public class PriceConfig
    {
        public int Id { get; set; }
        public string DayType { get; set; } = "Weekday"; // Weekday, Weekend, Holiday
        public decimal Surcharge { get; set; } // Phụ phí (Ví dụ: Lễ + 20,000đ)
        public string Description { get; set; } = string.Empty;
    }
}
