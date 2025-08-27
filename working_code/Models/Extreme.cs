namespace MBTP.Models
{
    public class ExtremeKey
    {
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public int expires_in { get; set; }
    }
    public class Device
    {
        public long? id { get; set; }
        public string? hostname { get; set; }
        public bool Connected { get; set; }
        public DateTime last_connect_time { get; set; }
        public long? location_id { get; set; }
        public string? hubName { get; set; }
    }
    public class DeviceList
    {
        public int page { get; set; }
        public int count { get; set; }
        public int total_pages { get; set; }
        public int total_count { get; set; }
        public List<Device> Data { get; set; } // Add this property to represent the nested devices listing
    }
    public class Floor
    {
        public long? id { get; set; }
        public string? name { get; set; }
    }
    public class FloorList
    {
        public int page { get; set; }
        public int count { get; set; }
        public int total_pages { get; set; }
        public int total_count { get; set; }
        public List<Floor> Data { get; set; } // Add this property to represent the nested devices listing
    }

}