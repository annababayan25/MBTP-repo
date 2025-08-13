using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MBTP.Models
{
    public class OccupancyReport
    {
        [JsonProperty("data")]
        public List<OccupancyData> Data { get; set; }
    }

    public class OccupancyData
    {
        [JsonProperty("category_name")]
        public string CategoryName { get; set; }

        // Assuming there are other properties here
        [JsonProperty("occupancy")]
        public Dictionary<DateTime, DailyOccupancyDetails> Occupancy { get; set; }
    }

    public class DailyOccupancyDetails
    {
        [JsonProperty("available")]
        public int Available { get; set; }

        [JsonProperty("occupied")]
        public int Occupied { get; set; }

        [JsonProperty("date")]
        public DateTime Date { get; set; }
    }

    public class DailyOccupancy
    {
        public DateTime Date { get; set; }
        public int TotalAvailable { get; set; }
        public int TotalOccupied { get; set; }
        public double OccupancyRate {get; set;}
    }
    public class FilteredOccupancyReport
    {
        public DateTime Month { get; set; }
        public List<DailyOccupancy> DailyOccupancies { get; set; }
        public List<DailyOccupants> DailyOccupants { get; set; }
        public string ErrorMessage { get; set; }
    }
}
