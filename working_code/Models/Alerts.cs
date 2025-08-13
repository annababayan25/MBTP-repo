namespace MBTP.Models
{
    public class Alerts
    {
        public DateTime TransDate { get; set; }
        public int PCID { get; set; }
        public string? Location { get; set; }
        public string? Severity { get; set; }
        public string? AlertText { get; set; }
    }
}
