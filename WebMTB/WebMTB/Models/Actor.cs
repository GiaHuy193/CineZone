using System.ComponentModel.DataAnnotations;
namespace WebMTB.Models
{
    public class Actor
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;

        public virtual ICollection<MovieActor> MovieActors { get; set; } = new List<MovieActor>();
    }
}
