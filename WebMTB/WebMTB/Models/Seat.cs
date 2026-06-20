namespace WebMTB.Models
{
    public class Seat
    {
        public int Id { get; set; }

        public string Row { get; set; } = string.Empty; // A, B, C

        public int Number { get; set; } // số ghế hiển thị: 1, 2, 3

        public string SeatType { get; set; } = "Normal";
        // Normal, VIP, Couple

        public int GridRow { get; set; } // vị trí dòng trong layout

        public int GridColumn { get; set; } // vị trí cột trong layout

        public int RoomId { get; set; }

        public Room? Room { get; set; }
    }
}