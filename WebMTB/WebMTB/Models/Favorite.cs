using Microsoft.AspNetCore.Identity;
namespace WebMTB.Models
{
    public class Favorite
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public virtual IdentityUser? User { get; set; }

        public int MovieId { get; set; }
        public virtual Movie? Movie { get; set; }
    }
}
