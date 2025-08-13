namespace MBTP.Models
{
    public class Guests
    {
        public string State { get; set; }
        // Add other properties if necessary
    }
    public class CustomFields
    {
        public string StoredMBTP { get; set; }
        public string StoredOutside { get; set; }
    }

    public class Booking
    {
        public int BookingID { get; set; }
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
        public string StateName { get; set; }
        public string CategoryName { get; set; }
        public string BookingCancelled { get; set; }
        public string ExpressCheckin { get; set; }
        public List<CustomFields>? CustomFields {get; set;}
    }
}