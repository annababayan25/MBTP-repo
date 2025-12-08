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
        public string? ItemDescription { get; set; }

        [JsonProperty("gl_account_description")]
        public string? GLAccountDescr { get; set; }

        [JsonProperty("item_date")]
        public DateTime? ItemDate { get; set; }

        [JsonProperty("reconciled_amount")]
        public decimal? Total_TaxInc { get; set; }

        [JsonProperty("reconciled_tax")]
        public decimal? Total_Tax { get; set; }

        public decimal? Total_TaxEx { get; set; }
        public decimal? TaxFreeTotal_TaxEx { get; set; }

        public decimal? FullSalesAccomTotal_TaxInc { get; set; }
        public decimal? FullSalesAccomTotal_Tax { get; set; }

        public decimal? ConcessionalSalesAccomTotal_TaxInc { get; set; }
        public decimal? ConcessionalSalesAccomTotal_Tax { get; set; }

        public decimal? PreparedFoodTotal_TaxInc { get; set; }
        public decimal? PreparedFoodTotal_Tax { get; set; }

        public decimal? GolfCartRentalTotal_TaxInc { get; set; }
        public decimal? GolfCartRentalTotal_Tax { get; set; }

        public decimal? GolfCartTaxTotal_TaxInc { get; set; }
        public decimal? GolfCartTaxTotal_Tax { get; set; }

        public decimal? AdmissionsTotal_TaxInc { get; set; }
        public decimal? AdmissionsTotal_Tax { get; set; }

        [JsonProperty("booking_id")]
        public int? BookingId { get; set; }

        [JsonProperty("account_for_name")]
        public string? AccountForName { get; set; }

        [JsonProperty("account_for_id")]
        public string? AccountForId { get; set; }

        public List<TransactionFlow>? TransactionFlows { get; set; }
    }

    public class NormalizedReconRow
    {
        public string GL { get; set; } = "";
        public string Client { get; set; } = "";
        public string Item { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime? ItemDate { get; set; }
    }

  public class SpecialReconGroup
    {
        public string GL { get; set; }
        public string Client { get; set; }
        public string Item { get; set; }
        public string Description { get; set; }

        public DateTime? ItemDate { get; set; }

        public decimal Amount { get; set; }
    }

    public class FyBucket
    {
        public DateTime FyStart { get; set; }

        public decimal Sites { get; set; }
        public decimal Rentals { get; set; }

        // NEW FIELD
        public decimal Lock_Fees { get; set; }
    }




}
