using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MBTP.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MBTP.Interfaces;
using System.Globalization;
using MBTP.Retrieval;
using System.Text.Json;
using System.Linq;

namespace MBTP.Services
{
    public class TransactionFlowApi : NewbookBaseApi
    {

        public TransactionFlowApi(HttpClient client) : base(client) { }

        public async Task<List<TransactionFlow>> PopulateTransactions(DateTime startDate, DateTime endDate)
        {
            var requestBody = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                return_all_data = "true"
            };

            var json = await PostAsync("reports_transaction_flow", requestBody);

            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var transactionFlow = new List<TransactionFlow>();

            foreach (var item in result.data)
            {
                var transactions = new TransactionFlow
                {
                    ItemId = item.item_id,
                    AccountFor = item.account_for,
                    AccountForId = item.account_for_id,
                    PaymentMethod = item.payment_transaction_method,
                    PaymentDescription = item.item_description,
                    PaymentTypeReference = item.payment_type_reference,
                    TranslatedPaymentType = item.translated_payment_type,
                    PaymentTypeAction = item.payment_type_action,
                    Category = item.category_name,
                    TransType = item.type,
                    TransDate = item.item_date,
                    ClientAccount = item.client_account,
                    GeneratedBy = item.user_name,
                    Description = item.description,
                    Amount = item.amount,
                    ArrivalDate = item.booking_period_from,
                    DepartureDate = item.booking_period_to,
                    Deposit = item.deposit,
                };

                if (item.item_type == "payments_raised")
                {
                    transactions.TransType = "Payments Raised";
                    transactions.PaymentTypeAction = "Payments";
                }
                if (item.item_type == "refunds_raised")
                {
                    transactions.TransType = "Refunds Raised";
                    transactions.PaymentTypeAction = "Refunds";
                }
                if (item.item_type == "payments_voided")
                {
                    transactions.TransType = "Voided Payments Voided";
                    transactions.PaymentTypeAction = "Payments";
                }
                if (item.item_type == "refunds_voided")
                {
                    transactions.TransType = "Voided Refunds Voided";
                    transactions.PaymentTypeAction = "Refunds";
                }

                if (item.payment_transaction_method == "cc_gateway")
                {
                    transactions.PaymentMethod = "Authorize.Net";
                }
                if (item.payment_transaction_method == "manual")
                {
                    transactions.PaymentMethod = "Manual Entry";
                }

                transactionFlow.Add(transactions);
                var jsonOutput = JsonConvert.SerializeObject(transactionFlow, Formatting.Indented);
                 File.WriteAllText("transFlow.json", jsonOutput);
                // File.AppendAllText("transFlow.txt", item.ToString() + Environment.NewLine);
            }
            
            return transactionFlow;
        }
        
    }
}
