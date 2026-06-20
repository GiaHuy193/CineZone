namespace WebMTB.Models
{
    public class CinemaChain
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;

        public virtual ICollection<Cinema> Cinemas { get; set; } = new List<Cinema>();
    }
}
