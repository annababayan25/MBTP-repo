using Newtonsoft.Json;

namespace MBTP.Models
{
    public class InventoryItems 
    {
        [JsonProperty("id")]
        public string Id {get;set;}

        [JsonProperty("name")]
        public string Name {get;set;}

        [JsonProperty("description")]
        public string? Description {get;set;}

        [JsonProperty("amount")]
        public decimal? Amount {get;set;}


    }
}