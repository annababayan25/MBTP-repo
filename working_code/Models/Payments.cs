using Newtonsoft.Json;

namespace MBTP.Models
{

    public class AppliedItems
    {
        [JsonProperty("charges")]
        public List<Charge> Charges { get; set; }

        [JsonProperty("credits")]
        public List<Credits> Credits { get; set; }
    }

    public class Taxes
    {
        [JsonProperty("tax_id")]
        public int? TaxId { get; set; }

        [JsonProperty("tax_name")]
        public string TaxName { get; set; }

        [JsonProperty("tax_inclusive")]
        public bool? TaxInclusive { get; set; }

        [JsonProperty("tax_amount")]
        public decimal? TaxAmount { get; set; }
    }


    public class Charge
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("account_id")]
        public int? AccountId { get; set; }

        [JsonProperty("gl_category_id")]
        public int? GlCategoryId { get; set; }

        [JsonProperty("gl_account_id")]
        public int? GlAccountId { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("tax_free")]
        public string TaxFree { get; set; }

        [JsonProperty("generated_by")]
        public int? GeneratedBy { get; set; }

        [JsonProperty("generated_when")]
        public DateTime? GeneratedWhen { get; set; }

        [JsonProperty("inventory_item_id")]
        public int? InventoryItemId { get; set; }

        [JsonProperty("voided_by")]
        public int? VoidedBy { get; set; }

        [JsonProperty("voided_when")]
        public DateTime? VoidedWhen { get; set; }

        [JsonProperty("link_type")]
        public string LinkType { get; set; }

        [JsonProperty("link_type_id")]
        public int? LinkTypeId { get; set; }

        [JsonProperty("link_period_from")]
        public DateTime? LinkPeriodFrom { get; set; }

        [JsonProperty("link_period_to")]
        public DateTime? LinkPeriodTo { get; set; }

        [JsonProperty("paid_amount")]
        public decimal? PaidAmount { get; set; }

        [JsonProperty("invoice_id")]
        public int? InvoiceId { get; set; }

        [JsonProperty("taxes")]
        public List<Tax> Taxes { get; set; }
    }

    public class Credits
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("account_id")]
        public int? AccountId { get; set; }

        [JsonProperty("gl_category_id")]
        public int? GlCategoryId { get; set; }

        [JsonProperty("gl_account_id")]
        public int? GlAccountId { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("tax_free")]
        public string TaxFree { get; set; }

        [JsonProperty("generated_by")]
        public int? GeneratedBy { get; set; }

        [JsonProperty("generated_when")]
        public DateTime? GeneratedWhen { get; set; }

        [JsonProperty("inventory_item_id")]
        public int? InventoryItemId { get; set; }

        [JsonProperty("voided_by")]
        public int? VoidedBy { get; set; }

        [JsonProperty("voided_when")]
        public DateTime? VoidedWhen { get; set; }

        [JsonProperty("link_type")]
        public string LinkType { get; set; }

        [JsonProperty("link_type_id")]
        public int? LinkTypeId { get; set; }

        [JsonProperty("link_period_from")]
        public DateTime? LinkPeriodFrom { get; set; }

        [JsonProperty("link_period_to")]
        public DateTime? LinkPeriodTo { get; set; }

        [JsonProperty("paid_amount")]
        public decimal? PaidAmount { get; set; }

        [JsonProperty("invoice_id")]
        public int? InvoiceId { get; set; }

        [JsonProperty("taxes")]
        public List<Tax> Taxes { get; set; }
    }
    

    public class Payments_Charges
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("account_id")]
        public int? AccountId { get; set; }

        [JsonProperty("account_for")]
        public string AccountFor { get; set; }

        [JsonProperty("account_for_id")]
        public int AccountForId { get; set; }

        [JsonProperty("account_for_name")]
        public string AccountForName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("generated_by")]
        public int? GeneratedBy { get; set; }

        [JsonProperty("generated_when")]
        public DateTime? GeneratedWhen { get; set; }

        [JsonProperty("voided_when")]
        public DateTime? VoidedWhen { get; set; }

        [JsonProperty("voided_by")]
        public int? VoidedBy { get; set; }
    
    }

    public class Refunds
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

    }

    public class Payments_Payments
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("account_id")]
        public int? AccountId { get; set; }

        [JsonProperty("account_for")]
        public string AccountFor { get; set; }

        [JsonProperty("account_for_id")]
        public int? AccountForId { get; set; }

        [JsonProperty("account_for_name")]
        public string AccountForName { get; set; }

        [JsonProperty("account_currency_id")]
        public int? AccountCurrencyId { get; set; }

        [JsonProperty("account_currency_code")]
        public string AccountCurrencyCode { get; set; }

        [JsonProperty("gl_category_id")]
        public int? GlCategoryId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("type_id")]
        public int? TypeId { get; set; }

        [JsonProperty("transaction_method")]
        public string TransactionMethod { get; set; }

        [JsonProperty("type_reference")]
        public string TypeReference { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("generated_by")]
        public int? GeneratedBy { get; set; }

        [JsonProperty("generated_when")]
        public DateTime? GeneratedWhen { get; set; }

        [JsonProperty("voided_when")]
        public DateTime? VoidedWhen { get; set; }

        [JsonProperty("voided_by")]
        public int? VoidedBy { get; set; }

        [JsonProperty("applied_items")]
        public AppliedItems AppliedItems { get; set; }

        [JsonProperty("charges")]
    public List<Charge> Charges { get; set; }

    [JsonProperty("credits")]
    public List<Credits> Credits { get; set; }

    [JsonProperty("taxes")]
        public List<Tax> Taxes { get; set; }

    public List<Payments_Charges> Charge { get; set; }
    

    }
}


