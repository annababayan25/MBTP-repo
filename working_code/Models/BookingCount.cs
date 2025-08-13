namespace MBTP.Models{
public class BookingCount
{
    public string State { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Count { get; set; }
}
    public class DailyBookingP
    {
        public DateTime Day { get; set; }
        public int Placed { get; set; }
    }
    public class DailyBookingC
    {
        public DateTime Day { get; set; }
        public int Cancelled { get; set; }
    }
    public class DailyBookingReport
    {
        public DateTime Month { get; set; }
        public List<DailyBookingP>DailyPlaced { get; set; }
        public List<DailyBookingC>DailyCancelled { get; set; }
    }
}