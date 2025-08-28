namespace MBTP.Models
{
    public class EquipmentFields
    {
        public string equipment_make { get; set; }
        public string equipment_model { get; set; }
        public string equipment_length { get; set; }
        public string equipment_registration { get; set; }
    }

    public class Guests
    {
        public string? State { get; set; }
        public string? Lastname { get; set; }
        public string? Firstname { get; set; }
        // Add other properties if necessary
    }
    public class CustomFields
    {
        public string Label { get; set; }
        public string Value { get; set; }
    }

    public class Booking
    {
        public int BookingID { get; set; }
        public string SiteName { get; set; }
        public string BookingArrival { get; set; }
        public string BookingDeparture { get; set; }
        public string BookingStatus { get; set; }
        public int BookingAdults { get; set; }
        public int BookingChildren { get; set; }
        public decimal BookingInfants { get; set; }
        public decimal BookingTotal { get; set; }
        public string BookingMethodName { get; set; }
        public string BookingSourceName { get; set; }
        public string BookingReasonName { get; set; }
        public decimal AccountBalance { get; set; }
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
        public string CarLicensePlate {get; set;}
        public string CarLicensePlateExtra { get; set; }
    }
}