using Newtonsoft.Json;

namespace MBTP.Models
{
    public class BookingsStaying
    {
        public string Category { get; set; }
        public string? BookingGroupId { get; set; }
        public string? BookingGroupName { get; set; }

        [JsonProperty("bookings")]
        public List<Bookings> Bookings { get; set; }
    }

    public class Bookings
    {
        [JsonProperty("bookings_group_id")]
        public string? BookingGroupId { get; set; }

        [JsonProperty("bookings_group_name")]
        public string? BookingGroupName { get; set; }
    }
}