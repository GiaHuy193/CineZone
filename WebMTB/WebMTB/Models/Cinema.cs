namespace WebMTB.Models
{
    public class Cinema
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}
