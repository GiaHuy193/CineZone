namespace WebMTB.Models
{
    public class Room
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CinemaId { get; set; }
        public Cinema? Cinema { get; set; }
        public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
    }
}
