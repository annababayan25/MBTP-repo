using Newtonsoft.Json;

namespace MBTP.Models
{
    public class EquipmentFields
    {
        public string? equipment_make { get; set; }
        public string? equipment_model { get; set; }
        public string? equipment_length { get; set; }
    }

    public class ContactDetail
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("content")]
        public string? Content { get; set; }

        [JsonProperty("notes")]
        public string? Notes { get; set; }

        [JsonProperty("allow_transactional")]
        public int AllowTransactional { get; set; }

        [JsonProperty("allow_marketing")]
        public int AllowMarketing { get; set; }
    }

    public class Guests
    {
        [JsonProperty("firstname")]
        public string? Firstname { get; set; }

        [JsonProperty("lastname")]
        public string? Lastname { get; set; }

        [JsonProperty("state")]
        public string? State { get; set; }

        [JsonProperty("contact_details")]
        public List<ContactDetail>? ContactDetails { get; set; }

        [JsonIgnore]
        public string? CarLicensePlate => ContactDetails?.FirstOrDefault(cd => cd.Type == "car_rego")?.Content;

        [JsonIgnore]
        public string? CarLicensePlateExtra => ContactDetails?.FirstOrDefault(cd => cd.Type == "car_rego")?.Notes;
    }

    public class CustomFields
    {
        public string? Label { get; set; }
        public string? Value { get; set; }
    }

    public class Booking
    {
        public int BookingID { get; set; }
        public string? SiteName { get; set; }
        public string? BookingArrival { get; set; }
        public string? BookingDeparture { get; set; }
        public string? BookingStatus { get; set; }
        public int BookingAdults { get; set; }
        public int BookingChildren { get; set; }
        public decimal BookingInfants { get; set; }
        public decimal BookingTotal { get; set; }
        public string? BookingMethodName { get; set; }
        public string? BookingSourceName { get; set; }
        public string? BookingReasonName { get; set; }
        public decimal AccountBalance { get; set; }
        public string? BookingPlaced { get; set; }
        public List<Guests>? Guests { get; set; }
        public string? StateName { get; set; }
        public string? CategoryName { get; set; }
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

        [JsonProperty("tariffs_quoted")]
        public List<TariffQuoted>? TariffsQuoted { get; set; }

        [JsonProperty("inventory_items")]
        public List<InventoryItem>? InventoryItems { get; set; }
        public decimal? Amount { get; set; }  // grand total of all payments for this booking
        public Dictionary<int, decimal> PaymentBreakdownById { get; set; } = new Dictionary<int, decimal>();


    }


    public class TariffQuoted
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("stay_date")]
        public string? StayDate { get; set; }

        [JsonProperty("label")]
        public string? Label { get; set; }

        [JsonProperty("original_amount")]
        public decimal OriginalAmount { get; set; }

        [JsonProperty("calculated_amount")]
        public decimal CalculatedAmount { get; set; }
    }

    public class OccupantCharge
    {
        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("occupants")]
        public int Occupants { get; set; }
    }

    public class InventoryItem
    {
        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("tax_free")]
        public string? TaxFree { get; set; }
    }

    public class Tax
    {
        [JsonProperty("tax_name")]
        public string? TaxName { get; set; }

        [JsonProperty("tax_amount")]
        public decimal TaxAmount { get; set; }

        [JsonProperty("tax_inclusive")]
        public bool TaxInclusive { get; set; }
    }

    public class Deposit
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("booking_id")]
        public string? BookingId { get; set; }


        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("original_amount")]
        public decimal OriginalAmount { get; set; }

        [JsonProperty("due_date")]
        public DateTime? DueDate { get; set; }

        [JsonProperty("from_type")]
        public string? FromType { get; set; }

        [JsonProperty("from_type_id")]
        public string? FromTypeId { get; set; }

        [JsonProperty("remove")]
        public string? Remove { get; set; }
    }

    public class AppliedItems
    {
        [JsonProperty("charges")]
        public List<Charges>? Charges { get; set; }

        [JsonProperty("credits")]
        public List<Credits>? Credits { get; set; }
    }

    public class Charges
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("account_id")]
        public string? AccountId { get; set; }

        [JsonProperty("link_period_from")]
        public string? PeriodFrom { get; set; }

        [JsonProperty("link_period_to")]
        public string? PeriodTo { get; set; }

        [JsonProperty("taxes")]
        public List<Tax>? Taxes { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }
    }

    public class Credits
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

    }

    public class Payment
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("account_id")]
        public string? AccountId { get; set; }

        [JsonProperty("account_for")]
        public string? AccountFor { get; set; }

        [JsonProperty("account_for_id")]
        public int? AccountForId { get; set; }

        [JsonProperty("account_for_name")]
        public string? AccountForName { get; set; }

        [JsonProperty("account_currency_code")]
        public string? CurrencyCode { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("transaction_method")]
        public string? TransactionMethod { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("link_period_from")]
        public decimal PeriodFrom { get; set; }

        [JsonProperty("link_period_to")]
        public decimal PeriodTo { get; set; }

        [JsonProperty("generated_when")]
        public DateTime GeneratedWhen { get; set; }

        [JsonProperty("voided_when")]
        public DateTime? VoidedWhen { get; set; }

        [JsonProperty("applied_items")]
        public List<AppliedItems>? AppliedItems { get; set; }

        [JsonProperty("charges")]
        public List<Charges>? Charges { get; set; }

        [JsonProperty("credits")]
        public List<Credits>? Credits { get; set; }

        public decimal? DepositsHeld { get; set; }

        [JsonProperty("gl_category_id")]
        public int? GlCategoryId { get; set; }

        [JsonProperty("gl_category_name")]
        public string? GlCategoryName { get; set; }   
        
        public bool? IsBookingDeposit { get; set; }
    }
    
    
}
