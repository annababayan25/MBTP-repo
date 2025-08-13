using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MBTP.Models
{
    public class OccupantReport
    {
        [JsonProperty("data")]
        public List<OccupantData> Data { get; set; }
    }

    public class OccupantData
    {
        [JsonProperty("booking_id")]
        public required string BookingId { get; set; }
        [JsonProperty("booking_arrival")]
        public required DateTime Bookingarrival { get; set; }
        [JsonProperty("booking_departure")]
        public required DateTime Bookingdeparture { get; set; }
        [JsonProperty("category_name")]
        public required string CategoryName { get; set; }
        [JsonProperty("booking_adults")]
        public required int Bookingadults { get; set; }
        [JsonProperty("booking_children")]
        public required int Bookingchildren { get; set; }
        [JsonProperty("booking_infants")]
        public required int Bookinginfants { get; set; }
       [JsonProperty("booking_source")]
        public required string Bookingsource { get; set; }
    }
    public class DailyOccupants
    {
        public DateTime Date { get; set; }
        public int TotalAdults { get; set; }
        public int TotalChildren { get; set; }
        public int TotalInfants {get; set;}
    }
}
