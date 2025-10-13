using Newtonsoft.Json;

namespace MBTP.Models
{
    public class TransactionFlow
    {
        [JsonProperty("item_id")]
        public string? ItemId { get; set; }

        [JsonProperty("payment_transaction_method")]
        public string? PaymentMethod { get; set; }

        [JsonProperty("item_description")]
        public string? PaymentDescription { get; set; }

        [JsonProperty("payment_type_reference")]
        public string? PaymentTypeReference { get; set; }

        [JsonProperty("grouped_payment_type")]
        public string? GroupedPaymentType { get; set; }

        public string? PaymentTypeAction { get; set; }

        [JsonProperty("category_name")]
        public string? Category { get; set; }

        [JsonProperty("item_type")]
        public string? TransType { get; set; }

        [JsonProperty("item_date")]
        public string? TransDate { get; set; }

        [JsonProperty("client_account")]
        public string? ClientAccount { get; set; }

        [JsonProperty("user_name")]
        public string? GeneratedBy { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("booking_period_from")]
        public string? ArrivalDate { get; set; }

        [JsonProperty("booking_period_to")]
        public string? DepartureDate { get; set; }

        [JsonProperty("deposit")]
        public string Deposit { get; set; }

    }
}