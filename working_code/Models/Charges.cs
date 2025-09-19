/*
using Newtonsoft.Json;

namespace MBTP.Models 
{
   public class TaxBreakdown
{
    [JsonProperty("tax_id")]
    public string TaxId { get; set; }

    [JsonProperty("tax_name")]
    public string TaxName { get; set; }

    [JsonProperty("tax_amount")]
    public decimal TaxAmount { get; set; }
}

public class Charges 
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("gl_account_code")]
    public string? GLAccountCode { get; set; }

    [JsonProperty("amount")]
    public decimal? Amount { get; set; }

    [JsonProperty("amount_inc_tax")]
    public decimal? AmountIncTax { get; set; }

    [JsonProperty("amount_ex_tax")]
    public decimal? AmountExTax { get; set; }

    [JsonProperty("tax")]
    public decimal? Tax { get; set; }

    [JsonProperty("tax_free")]
    public string? TaxFree { get; set; }

    [JsonProperty("account_for_name")]
    public string? AccountForName { get; set; }

    [JsonProperty("account_for_id")]
    public string? AccountForId { get; set; }

    [JsonProperty("tax_breakdown")]
    public List<TaxBreakdown> TaxBreakdown { get; set; } = new();
}

}
*/