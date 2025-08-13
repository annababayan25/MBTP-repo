using Microsoft.VisualBasic;

namespace MBTP.Models
{
    public class Payments
    {
        public string? PaymentType { get; set; }
        // Add other properties if necessary
        public decimal PaymentAmount { get; set; }
    }

    public class RetailGroup
    {
        public int subtotal_level { get; set; }
        public decimal net_sales { get; set; }
        public string? category { get; set; }
        public string? subcategory { get; set; }
    }
    public class PaymentsGroup
    {
        public decimal net_payments { get; set; }
        public string? payment_type { get; set; }
    }
}