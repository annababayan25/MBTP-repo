using Newtonsoft.Json;

namespace MBTP.Models
{
    public class CheckedIn
    {
        public int BookingID { get; set; }
        public string SiteName { get; set; }
        public string BookingArrival { get; set; }
        public string BookingDeparture { get; set; }
        public string BookingStatus { get; set; }
        public int BookingAdults { get; set; }
        public int BookingChildren { get; set; }
        public decimal BookingInfants { get; set; }
        public decimal? BookingTotal { get; set; }
        public string BookingMethodName { get; set; }
        public string BookingSourceName { get; set; }
        public string BookingReasonName { get; set; }
        public decimal? AccountBalance { get; set; }
        public string BookingPlaced { get; set; }
        public List<Guests> Guests { get; set; } // Add this property to represent the nested guests object
        public string? StateName { get; set; }
        public string? CategoryName { get; set; }
        public string BookingCancelled { get; set; }
        public string ExpressCheckin { get; set; }
        public List<CustomFields> CustomFields { get; set; } // Add this property to represent the nested custom fields object
        public string? StoredMBTP { get; set; }
        public string? StoredOutside { get; set; }
        public List<EquipmentFields> Equipment { get; set; }
        public string? EquipmentMake { get; set; }
        public string? EquipmentModel { get; set; }
        public string? EquipmentLength { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public int Wristbands { get; set; }
        public string? CarLicensePlate { get; set; }
        public string? CarLicensePlateExtra { get; set; }
        public decimal? LockFee { get; set; }

        [JsonProperty("tariffs_quoted")]
        public List<TariffQuoted> TariffsQuoted { get; set; }

        [JsonProperty("deposits")]
        public List<Deposit> Deposits { get; set; }

        [JsonProperty("inventory_items")]
        public List<InventoryItem> InventoryItems { get; set; }
        public decimal?  CalculatedStayCost { get; set; }
        public decimal? DepositsHeld { get; set; }
    }
    
    public class TariffQuoted
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("stay_date")]
        public string StayDate { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

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
        public string Description { get; set; }

        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        [JsonProperty("tax_free")]
        public string TaxFree { get; set; }
    }


    public class Tax
    {
        [JsonProperty("tax_name")]
        public string TaxName { get; set; }

        [JsonProperty("tax_amount")]
        public decimal TaxAmount { get; set; }

        [JsonProperty("tax_inclusive")]
        public bool TaxInclusive { get; set; }
    }

    public class Deposit
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("booking_id")]
        public string BookingId { get; set; }

        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        [JsonProperty("original_amount")]
        public decimal OriginalAmount { get; set; }

        [JsonProperty("due_date")]
        public DateTime DueDate { get; set; }

        [JsonProperty("from_type")]
        public string FromType { get; set; }

        [JsonProperty("from_type_id")]
        public string FromTypeId { get; set; }

        [JsonProperty("remove")]
        public string Remove { get; set; }
    }



}