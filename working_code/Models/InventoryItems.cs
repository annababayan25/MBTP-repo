using Newtonsoft.Json;

namespace MBTP.Models
{
    public class InventoryItems 
    {
        [JsonProperty("gl_account_id")]
        public string? GlAccountId { get; set; }
        
        [JsonProperty("gl_category_id")]
        public string? GlCategoryId {get;set;}

        [JsonProperty("name")]
        public string? Name {get;set;}

        [JsonProperty("description")]
        public string? Description {get;set;}

        [JsonProperty("amount")]
        public decimal? Amount {get;set;}


    }
}