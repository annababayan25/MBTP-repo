using Newtonsoft.Json;

namespace MBTP.Models 
{
    public class Recon 
    {

        [JsonProperty("gl_account_code")]
        public string? GLAccount {get;set;}

        [JsonProperty("item_type")]
        public string? ItemType {get;set;}

        [JsonProperty("item_description")]
        public string? ItemDescription {get;set;}

        [JsonProperty("item_date")]
        public DateTime ItemDate {get;set;}

        [JsonProperty("reconciled_amount")]
        public decimal? ReconAmount {get;set;}

        [JsonProperty("reconciled_tax")]
        public decimal? ReconTax {get;set;}

        public List<TransactionFlow>? TransactionFlows { get; set; }

        [JsonProperty("client_account")]
        public string? ClientAccount { get; set; }

        [JsonProperty("account_for_id")]
        public string? AccountForId { get; set; }


    }
}