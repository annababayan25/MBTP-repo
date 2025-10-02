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
    public class TransactionFlowApi
    {
        private readonly string apiUrl = "https://api.newbook.cloud/rest/reports_transaction_flow";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly IDatabaseConnectionService _dbConnectionService;
        public TransactionFlowApi(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }


        public async Task PopulateTransactions(DateTime startDate, DateTime endDate)
        {
            Console.WriteLine("Run method started.");

            var transactions = await FetchAllTransactionsAsync(startDate, endDate);

            if (transactions.Count > 0)
            {
                using SqlConnection sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                // Insert bookings
                foreach (var transaction in transactions)
                {
                    await InsertTransactionFlowTable(transaction, sqlConn);
                }

                Console.WriteLine("Total Transactions: " + transactions.Count);
            }
            else
            {
                Console.WriteLine("No transactions to display.");
            }

            Console.WriteLine("Run method finished.");
        }

        private async Task InsertTransactionFlowTable(TransactionFlow transaction, SqlConnection sqlConn)
        {
            using (SqlCommand cmd = new SqlCommand(@"dbo.UpdateTransactionFlowTable", sqlConn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@PaymentMethod", $"{transaction.PaymentMethod} {transaction.GroupedPaymentType} {transaction.PaymentTypeAction} - For {Convert.ToDateTime(transaction.TransDate).ToString("MMM dd yyyy")}");
                cmd.Parameters.AddWithValue("@Category", transaction.Category ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@TransNumber", transaction.PaymentTypeReference != null ? $"{transaction.TransType} #{transaction.ItemId} (Ref #{transaction.PaymentTypeReference})" : $"{transaction.TransType} #{transaction.ItemId}");
                cmd.Parameters.AddWithValue("@TransDate", transaction.TransDate);
                cmd.Parameters.AddWithValue("@ClientAccount", transaction.ClientAccount);
                cmd.Parameters.AddWithValue("@GeneratedBy", transaction.GeneratedBy);
                cmd.Parameters.AddWithValue("@Description", transaction.Description);
                cmd.Parameters.AddWithValue("@Amount", transaction.Amount ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ArrivalDate", transaction.ArrivalDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DepartureDate", transaction.DepartureDate ?? (object)DBNull.Value);
                cmd.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<TransactionFlow>> FetchAllTransactionsAsync(DateTime startDate, DateTime endDate)
        {
            var periodFrom = startDate.ToString("yyyy-MM-dd HH:mm:ss");
            var periodTo = endDate.ToString("yyyy-MM-dd HH:mm:ss");
            var transactionFlow = new List<TransactionFlow>();

            var requestBody = new
            {
                region = region,
                api_key = apiKey,
                period_from = periodFrom,
                period_to = periodTo,
                return_all_data = true
            };

            int loopCount = 0;
            HttpResponseMessage response = new HttpResponseMessage();
            while (loopCount < 5)
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
                var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                response = await httpClient.PostAsync(apiUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                    loopCount++;
                    if (loopCount == 5)
                    {
                        return transactionFlow;
                    }
                }
                else
                {
                    break;
                }
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            JObject jsonObject = JObject.Parse(jsonResponse);
            List<string> jsonTokens = new List<string>();

            var result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
            //Console.WriteLine($"HTTP JSON RESPONSE: {jsonResponse}");
            if (result is null || result.success != "true")
            {
                Console.WriteLine("API response indicates failure.");
                return new List<TransactionFlow>();
            }

            if (result != null)
            {
                if (result.data != null)
                {
                    foreach (var item in result.data)
                    {
                        Console.WriteLine("--------------------------------------------------------------");
                        Console.WriteLine("ItemId: " + item.ItemId?.ToString());
                        Console.WriteLine("PaymentMethod: " + item.payment_transaction_method?.ToString());
                        Console.WriteLine("PaymentDescription: " + item.item_description?.ToString());
                        Console.WriteLine("PaymentEntryType: " + item.entry_type?.ToString());
                        Console.WriteLine("PaymentTypeReference: " + item.payment_type_reference?.ToString());
                        Console.WriteLine("Category: " + item.category_name?.ToString());
                        Console.WriteLine("TransType: " + item.item_type?.ToString());
                        Console.WriteLine("TransDate: " + item.item_date?.ToString());
                        Console.WriteLine("ClientAccount: " + item.client_account?.ToString());
                        Console.WriteLine("GeneratedBy: " + item.user_name?.ToString());
                        Console.WriteLine("Description: " + item.description?.ToString());
                        Console.WriteLine("ArrivalDate: " + item.booking_period_from?.ToString());
                        Console.WriteLine("DepartureDate: " + item.booking_period_to?.ToString());
                        Console.WriteLine("Deposit: " + item.deposit?.ToString());
                        Console.WriteLine("--------------------------------------------------------------");

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


                        transactionFlow.Add(transactions);
                    }
                }
            }
            return transactionFlow;

        }
    }
}