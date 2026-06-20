using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebMTB.Models
{
    public class Review
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Vui lòng đánh giá từ 1 đến 5 sao")]
        public int Rating { get; set; } // Số sao người dùng chọn

        [Required(ErrorMessage = "Vui lòng nhập nội dung bình luận")]
        [StringLength(1000)]
        public string Comment { get; set; } = string.Empty; // Nội dung bình luận

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // --- Khóa ngoại ---

        // Liên kết tới Phim
        public int MovieId { get; set; }
        [ForeignKey("MovieId")]
        public virtual Movie? Movie { get; set; }

        // Liên kết tới Người dùng (Dùng IdentityUser mặc định của ASP.NET)
        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public virtual IdentityUser? User { get; set; }
    }
}