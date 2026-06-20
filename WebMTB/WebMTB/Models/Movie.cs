using System.ComponentModel.DataAnnotations;
namespace WebMTB.Models
{
    public class Movie
    {
        public int Id { get; set; }
        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Director { get; set; } = string.Empty;
        public string Cast { get; set; } = string.Empty; // Danh sách diễn viên
        public int Duration { get; set; } // Phút
        public DateTime ReleaseDate { get; set; }
        public string TrailerUrl { get; set; } = string.Empty;
        public string? AgeRating { get; set; } // Lưu các giá trị như: "P", "13+", "16+", "18+"
        public string ImageUrl { get; set; } = string.Empty;
        public string CoverImageUrl { get; set; } = string.Empty;
        public double Rating { get; set; } // Điểm đánh giá trung bình
        public bool IsHot { get; set; } // Phim Hot
        public bool IsActive { get; set; } // Phim đang chiếu hay đã ngừng

        // Quan hệ
        public virtual ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
