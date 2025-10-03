using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MBTP.Models
{

    public class OccReport
    {
        [JsonProperty("category_name")]
        public string CategoryName { get; set; }

        [JsonProperty("occupancy")]
        public Dictionary<string, OccDetails> Occupancy { get; set; }

        [JsonProperty("available")]
        public int? Available { get; set; }

        [JsonProperty("occupied")]
        public int? Occupied { get; set; }

        [JsonProperty("date")]
        public string? OccupancyDate { get; set; }

        [JsonProperty("maintenance")]
        public int? Maintenance { get; set; }

        [JsonProperty("allotted")]
        public int? Allotted { get; set; }

        [JsonProperty("revenue_gross")]
        public decimal? RevenueGross { get; set; }

        [JsonProperty("revenue_net")]
        public decimal? RevenueNet { get; set; }
        public int? Sites { get; set; }
    }

    public class OccDetails
    {
        [JsonProperty("available")]
        public int? Available { get; set; }

        [JsonProperty("occupied")]
        public int? Occupied { get; set; }

        [JsonProperty("date")]
        public string? OccupancyDate { get; set; }

        [JsonProperty("maintenance")]
        public int? Maintenance { get; set; }

        [JsonProperty("allotted")]
        public int? Allotted { get; set; }

        [JsonProperty("revenue_gross")]
        public decimal? RevenueGross { get; set; }

        [JsonProperty("revenue_net")]
        public decimal? RevenueNet { get; set; }

    }
}