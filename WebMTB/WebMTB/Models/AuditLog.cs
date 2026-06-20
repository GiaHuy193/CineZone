namespace WebMTB.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Thêm, Sửa, Xóa
        public string EntityName { get; set; } = string.Empty; // Movie, Cinema...
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Details { get; set; } = string.Empty;
    }
}
