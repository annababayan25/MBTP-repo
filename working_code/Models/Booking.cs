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
        [JsonProperty("guest_id")]
        public string GuestId { get; set; } = string.Empty;

        [JsonProperty("firstname")]
        public string? Firstname { get; set; } = string.Empty;

        [JsonProperty("lastname")]
        public string? Lastname { get; set; } = string.Empty;

        [JsonProperty("state")]
        public string? State { get; set; }

        [JsonProperty("contact_details")]
        public List<ContactDetail> ContactDetails { get; set; } = new List<ContactDetail>();

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
        public List<Guests> Guests { get; set; } = new List<Guests>();
        public string? StateName { get; set; }
        public string? CategoryName { get; set; }
        public string? BookingCancelled { get; set; }
        public string? ExpressCheckin { get; set; }
        public List<CustomFields> CustomFields { get; set; } = new List<CustomFields>();
        public string? StoredMBTP { get; set; }
        public string? StoredOutside { get; set; }
        public List<EquipmentFields> Equipment { get; set; } = new List<EquipmentFields>();
        public string? EquipmentMake { get; set; }
        public string? EquipmentModel { get; set; }
        public string? EquipmentLength { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public int Wristbands { get; set; }
        public string? CarLicensePlate { get; set; }
        public string? CarLicensePlateExtra { get; set; }

    }

}