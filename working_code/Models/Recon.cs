using Newtonsoft.Json;

namespace MBTP.Models 
{
    public class Recon 
    {
        [JsonProperty("gl_account_id")]
        public string? GLAccountId { get; set; }

        [JsonProperty("gl_account_code")]
        public string? GLAccountCode { get; set; }

        [JsonProperty("item_description")]
        public string? ItemDescription {get;set;}

        [JsonProperty("gl_account_description")]
        public string? GLAccountDescr {get;set;}

        [JsonProperty("item_date")]
        public DateTime ItemDate {get;set;}

        [JsonProperty("reconciled_amount")]
        public decimal? ReconAmount {get;set;}

        [JsonProperty("reconciled_tax")]
        public decimal? ReconTax {get;set;}

        public List<TransactionFlow>? TransactionFlows { get; set; }

        [JsonProperty("booking_id")]
        public int? BookingId { get; set; }

        [JsonProperty("account_for_name")]
        public string? AccountForName { get; set; }

        [JsonProperty("account_for_id")]
        public string? AccountForId{ get; set; }

        public decimal? TotalTaxEx {get;set;}

        public decimal? GolfCartTax_Total_TaxInc {get;set;}
        
        public decimal? GolfCartTax_Tax {get;set;}

    }
}