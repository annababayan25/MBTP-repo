using Newtonsoft.Json;

namespace MBTP.Models
{

    public class CheckedIn
    {
        public int BookingId { get; set; }
        public string? SiteName { get; set; }
        public DateTime? BookingArrival { get; set; }
        public DateTime? BookingDeparture { get; set; }
        public string? BookingStatus { get; set; }
        public int BookingAdults { get; set; }
        public int? BookingChildren { get; set; }
        public decimal? BookingInfants { get; set; }
        public decimal? BookingTotal { get; set; }
        public string? BookingMethodName { get; set; }
        public string? BookingSourceName { get; set; }
        public string? BookingReasonName { get; set; }
        public decimal? AccountBalance { get; set; }
        public string? BookingPlaced { get; set; }
        public List<Guests>? Guests { get; set; }
        public string? StateName { get; set; }
        public string? BookingCancelled { get; set; }
        public string? ExpressCheckin { get; set; }
        public List<CustomFields>? CustomFields { get; set; }
        public string? StoredMBTP { get; set; }
        public string? StoredOutside { get; set; }
        public List<EquipmentFields>? Equipment { get; set; }
        public string? EquipmentMake { get; set; }
        public string? EquipmentModel { get; set; }
        public string? EquipmentLength { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public int Wristbands { get; set; }
        public string? CarLicensePlate { get; set; }
        public string? CarLicensePlateExtra { get; set; }
        public string? BookingName { get; set; }
        public decimal? CalculatedStayCost { get; set; }
        public decimal? DepositsHeld { get; set; }
        public decimal? Amount { get; set; }
        [JsonProperty("charges")]
        public List<Charges>? Charges { get; set; }
        [JsonProperty("tariffs_quoted")]
        public List<TariffQuoted>? TariffsQuoted { get; set; }
        [JsonProperty("inventory_items")]
        public List<InventoryItem>? InventoryItems { get; set; }
        public Dictionary<int, decimal> PaymentBreakdownById { get; set; } = new Dictionary<int, decimal>();
        [JsonProperty("payments")]
        public List<Payment>? Payments { get; set; }
        [JsonProperty("refunds")]
        public List<Refund>? Refunds { get; set; }
        [JsonProperty("credits")]
        public List<Credit>? Credits { get; set; }
        [JsonProperty("taxes")]
        public List<Tax>? Taxes { get; set; }
        [JsonProperty("booking_checkedin")]
        public DateTime? BookingCheckedIn { get; set; }
        [JsonProperty("category_name")]
        public string? CategoryName { get; set; }
        [JsonProperty("site_name")]
        public string? Site { get; set; }
        public decimal? SecurityDeposits { get; set; }
        public decimal? LockFee { get; set; }
        public decimal? OnlineBookingFee { get; set; }
        public decimal? RefundedAmount { get; set; }
        public decimal? CancellationFee { get; set; }
        public decimal? PaymentsAfterCheckIn { get; set; }
        public string? PaymentsAfterCheckInDesc { get; set; }
        public string? Extras { get; set; }

    }


    public class TariffQuoted
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("stay_date")]
        public string? StayDate { get; set; }

        [JsonProperty("label")]
        public string? Label { get; set; }

        [JsonProperty("original_amount")]
        public decimal OriginalAmount { get; set; }

        [JsonProperty("calculated_amount")]
        public decimal CalculatedAmount { get; set; }

        [JsonProperty("taxes")]
        public List<Tax>? Taxes { get; set; }
    }

    public class InventoryItem
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("inventory_item_id")]
        public int? InventoryItemId { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("amount")]
        public string? Amount { get; set; }

        [JsonProperty("tax_free")]
        public string? TaxFree { get; set; }
    }

    public class Tax
    {
        [JsonProperty("tax_name")]
        public string? TaxName { get; set; }

        [JsonProperty("tax_amount")]
        public decimal? TaxAmount { get; set; }

        [JsonProperty("tax_inclusive")]
        public bool? TaxInclusive { get; set; }
    }


    public class Charges
    {

        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("account_id")]
        public string? AccountId { get; set; }

        [JsonProperty("inventory_item_id")]
        public int? InventoryItemId { get; set; }

        [JsonProperty("link_period_from")]
        public string? PeriodFrom { get; set; }

        [JsonProperty("link_period_to")]
        public string? PeriodTo { get; set; }

        [JsonProperty("taxes")]
        public List<Tax>? Taxes { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("generated_when")]
        public DateTime? GeneratedWhen { get; set; }

        [JsonProperty("voided_when")]
        public DateTime? VoidedWhen { get; set; }
    }

    public class Credit
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("account_id")]
        public string? AccountId { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("taxes")]
        public List<Tax>? Taxes { get; set; }

        [JsonProperty("voided_when")]
        public string? VoidedWhen { get; set; }

    }

    public class Payment
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("account_id")]
        public string? AccountId { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("transaction_method")]
        public string? TransactionMethod { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("link_period_from")]
        public decimal? PeriodFrom { get; set; }

        [JsonProperty("link_period_to")]
        public decimal? PeriodTo { get; set; }

        [JsonProperty("generated_when")]
        public DateTime? GeneratedWhen { get; set; }

        [JsonProperty("voided_when")]
        public DateTime? VoidedWhen { get; set; }

        [JsonProperty("charges")]
        public List<Charges>? Charges { get; set; }

        [JsonProperty("credits")]
        public List<Credit>? Credits { get; set; }

        public decimal? DepositsHeld { get; set; }

        [JsonProperty("gl_category_id")]
        public int? GlCategoryId { get; set; }

        [JsonProperty("gl_category_name")]
        public string? GlCategoryName { get; set; }

        public bool? IsBookingDeposit { get; set; }

        [JsonProperty("deposit")]
        public string? Deposit { get; set; }

        [JsonProperty("payment_charges")]
        public List<PaymentChargeLink>? PaymentCharges { get; set; }

        public decimal PaymentsAfterCheckIn { get; set; }
    }

    public class Refund
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("generated_when")]
        public DateTime? GeneratedWhen { get; set; }

    }

    public class PaymentChargeLink
    {
        [JsonProperty("link_id")]
        public int LinkId { get; set; }

        [JsonProperty("charge_id")]
        public int ChargeId { get; set; }

        [JsonProperty("reconciled_amount")]
        public decimal ReconciledAmount { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("voided_when")]
        public DateTime? VoidedWhen { get; set; }
    }

}
