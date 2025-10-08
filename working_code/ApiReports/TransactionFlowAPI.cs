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

namespace MBTP.Services
{
    public class TransactionFlowApi : NewbookBaseApi
    {
        private readonly IDatabaseConnectionService _dbConnectionService;
        public TransactionFlowApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }


        public async Task PopulateTransactions(DateTime startDate, DateTime endDate)
        {
            var body = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                return_all_data = true
            };

            var json = await PostAsync("reports_transaction_flow", body);

            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var transactionFlow = new List<TransactionFlow>();

            foreach (var item in result.data)
            {
                var transactions = new TransactionFlow
                {
                    ItemId = item.item_id,
                    PaymentMethod = item.payment_transaction_method,
                    PaymentDescription = item.item_description,
                    PaymentTypeReference = item.payment_type_reference,
                    GroupedPaymentType = item.grouped_payment_type,
                    PaymentTypeAction = item.payment_type_action,
                    Category = item.category_name,
                    TransType = item.item_type,
                    TransDate = item.item_date,
                    ClientAccount = item.client_account,
                    GeneratedBy = item.user_name,
                    Description = item.description,
                    Amount = item.amount,
                    ArrivalDate = item.booking_period_from,
                    DepartureDate = item.booking_period_to,
                };

                if (item.item_type == "payments_raised")
                {
                    transactions.TransType = "Payments Raised";
                    transactions.PaymentTypeAction = "Payments";
                }
                if (item.item_type == "refunds_raised")
                {
                    transactions.TransType = "Refund Raised";
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

                if (item.grouped_payment_type == "visa" || item.grouped_payment_type == "discover" || item.grouped_payment_type == "cash")
                {
                    transactions.GroupedPaymentType = CultureInfo.CurrentCulture.TextInfo.ToTitleCase((string)item.grouped_payment_type);
                }
                if (item.grouped_payment_type == "mastercard")
                {
                    transactions.GroupedPaymentType = "MasterCard";
                }
                if (item.grouped_payment_type == "amex")
                {
                    transactions.GroupedPaymentType = "AMEX";
                }
                if (item.grouped_payment_type == "cheque")
                {
                    transactions.GroupedPaymentType = "Check";
                }
                if (item.grouped_payment_type == "balance_transfer")
                {
                    transactions.GroupedPaymentType = "Balance Transfer";
                }

                string filename = "output.txt";
                File.AppendAllText(filename, item.ToString() + Environment.NewLine);

                transactionFlow.Add(transactions);
            }

            using var sqlConn = _dbConnectionService.CreateConnection();
            await sqlConn.OpenAsync();

            foreach (var transactions in transactionFlow)
            {
                using (SqlCommand cmd = new SqlCommand(@"dbo.UpdateTransactionFlowTable", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@PaymentMethod", $"{transactions.PaymentMethod} {transactions.GroupedPaymentType} {transactions.PaymentTypeAction} - For {Convert.ToDateTime(transactions.TransDate).ToString("MMM dd yyyy")}");
                    cmd.Parameters.AddWithValue("@Category", transactions.Category ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TransNumber", transactions.PaymentTypeReference != null ? $"{transactions.TransType} #{transactions.ItemId} (Ref #{transactions.PaymentTypeReference})" : $"{transactions.TransType} #{transactions.ItemId}");
                    cmd.Parameters.AddWithValue("@TransDate", transactions.TransDate);
                    cmd.Parameters.AddWithValue("@ClientAccount", transactions.ClientAccount);
                    cmd.Parameters.AddWithValue("@GeneratedBy", transactions.GeneratedBy);
                    cmd.Parameters.AddWithValue("@Description", transactions.Description);
                    cmd.Parameters.AddWithValue("@Amount", transactions.Amount ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ArrivalDate", transactions.ArrivalDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DepartureDate", transactions.DepartureDate ?? (object)DBNull.Value);
                    cmd.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            Console.WriteLine("Run method finished.");
            Console.WriteLine("Total Transactions: " + transactionFlow.Count);
            transactionFlow.Clear();

        }
        
    }
}
