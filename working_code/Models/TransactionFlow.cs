using Newtonsoft.Json.Linq;

namespace MBTP.Models
{
    public class TransactionFlow
    {
        public string PaymentMethod { get; set; }
        public string? Category { get; set; }
        public string TransNumber { get; set; }
        public DateTime TransDate { get; set; }
        public string ClientAccount { get; set; }
        public string GeneratedBy { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string? ArrivalDate { get; set; }
        public string? DepartureDate { get; set; }
    }
}