using Newtonsoft.Json;

namespace MBTP.Models
{
    public class GLAccount
    {
        [JsonProperty("gl_account_id")]
        public string? GLAccountId { get; set; }

        [JsonProperty("gl_account_code")]
        public string? GLAccountCode { get; set; }

        [JsonProperty("gl_account_name")]
        public string? GLAccountName { get; set; }

        [JsonProperty("long_description")]
        public string? LongDescription { get; set; }

        [JsonProperty("refundable")]
        public string? Refundable { get; set; }

        [JsonProperty("gl_group_id")]
        public string? GLGroupId { get; set; }

        [JsonProperty("gl_group_name")]
        public string? GLGroupName { get; set; }

        [JsonProperty("active")]
        public string? Active { get; set; }
    }
}
