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
                return_all_data = "true"
            };

            var json = await PostAsync("reports_transaction_flow", body);

            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var transactionFlow = new List<TransactionFlow>();

            foreach (var item in result.data)
            {
                var transactions = new TransactionFlow
                {
                    ItemId = item.item_id,
                    AccountFor = item.account_for,
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

                if (item.payment_transaction_method == "cc_gateway")
                {
                    transactions.PaymentMethod = "Authorize.Net";
                }
                if (item.payment_transaction_method == "manual")
                {
                    transactions.PaymentMethod = "Manual Entry";
                }


                transactionFlow.Add(transactions);
                var outputFile = "output.txt";
                File.AppendAllText(outputFile, item.ToString() + Environment.NewLine);
            }

            var depositsHeldSites = transactionFlow
            .Where(p =>
                p.Category != null &&
                (
                    p.Category.Contains("WESC", StringComparison.OrdinalIgnoreCase) ||
                    p.Category.Contains("Security Deposit", StringComparison.OrdinalIgnoreCase)
                ) &&
                p.Deposit != null &&
                p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase) &&
                (
                    p.Description == null ||
                    !p.Description.Contains("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase)
                ) 
            )
            .Sum(p => Math.Abs(p.Amount ?? 0));

            var depositsHeldRentals = transactionFlow
            .Where(p =>
                p.Category != null &&
                (p.Category.Contains("Ocean Villa", StringComparison.OrdinalIgnoreCase) ||
                 p.Category.Contains("Cottage", StringComparison.OrdinalIgnoreCase) ||
                 p.Category.Contains("Cabin", StringComparison.OrdinalIgnoreCase) ||
                 p.Category.Contains("Travel Trailer - Mid Beach", StringComparison.OrdinalIgnoreCase) ||
                 p.Category.Contains("Security Deposit", StringComparison.OrdinalIgnoreCase)) &&
                p.Deposit != null &&
                p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase) &&
                (
                    p.Description == null ||
                    !p.Description.Equals("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase)
                )
            )
            .Sum(p => Math.Abs(p.Amount ?? 0));

            var depositsHeldSitesCount = transactionFlow
            .Where(p =>
                p.Category != null &&
                (p.Category.Contains("WESC", StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains("Security Deposit", StringComparison.OrdinalIgnoreCase)) &&
                p.Deposit != null &&
                p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase) &&
                (
                    p.Description == null ||
                    !p.Description.Contains("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase)
                )
            )
            .Count();

            var depositsHeldRentalsCount = transactionFlow
            .Where(p =>
                p.Category != null &&
                (p.Category.Contains("Ocean Villa", StringComparison.OrdinalIgnoreCase) ||
                 p.Category.Contains("Cottage", StringComparison.OrdinalIgnoreCase) ||
                 p.Category.Contains("Cabin", StringComparison.OrdinalIgnoreCase) ||
                 p.Category.Contains("Travel Trailer - Mid Beach", StringComparison.OrdinalIgnoreCase) ||
                 p.Category.Contains("Security Deposit", StringComparison.OrdinalIgnoreCase)) &&
                p.Deposit != null &&
                p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase) &&
                (
                    p.Description == null ||
                    !p.Description.Contains("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase)
                )
            ).Count();

            bool hasMatchingDate = transactionFlow.Any(p =>
            {
                if (p.TransDate == null) return false;
                var dateValue = Convert.ToDateTime(p.TransDate);
                return dateValue.Date == DateTime.Today.AddDays(-7).Date;
            });

            if (hasMatchingDate)
            {
                var outputFile = "outputTF.txt";
                var sites = $"Deposits Held Sites ({depositsHeldSitesCount}) (for {DateTime.Today.AddDays(-7):MMM dd yyyy}): {depositsHeldSites:C}{Environment.NewLine}";
                var rentals = $"Deposits Held Rentals ({depositsHeldRentalsCount}) (for {DateTime.Today.AddDays(-7):MMM dd yyyy}): {depositsHeldRentals:C}{Environment.NewLine}";
                File.AppendAllText(outputFile, sites + rentals);
                Console.WriteLine(" Deposits held total written to outputTF.txt");
            }
            else
            {
                Console.WriteLine("No transactions found for target date, no output written.");
            }
            // Filter the list for deposits held transactions (same conditions as above)
            var depositsHeldList = transactionFlow
                .Where(p =>
                    p.Category != null &&
                    (
                        p.Category.Contains("WESC", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Security Deposit", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Ocean Villa", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Cottage", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Cabin", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Travel Trailer - Mid Beach", StringComparison.OrdinalIgnoreCase)
                    ) &&
                    p.Deposit != null &&
                    p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase) &&
                    (
                        p.Description == null ||
                        !p.Description.Contains("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase)
                    ) 
                )
                .ToList();

            // Convert to JSON 
            var jsonOutput = JsonConvert.SerializeObject(depositsHeldList, Formatting.Indented);

            // Write to file
            var jsonFile = "depositsHeld.json";
            File.WriteAllText(jsonFile, jsonOutput);
            Console.WriteLine($"Deposits held JSON written to {jsonFile}");

            
            using var sqlConn = _dbConnectionService.CreateConnection();
            await sqlConn.OpenAsync();

            foreach (var transactions in transactionFlow)
            {
                var dateValue = Convert.ToDateTime(transactions.TransDate);
                var stringValue = dateValue.ToString("MMM dd yyyy hh:mm tt");

                using (SqlCommand cmd = new SqlCommand(@"dbo.UpdateTransactionFlowTable", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@PaymentMethod", $"{transactions.PaymentMethod} {transactions.TranslatedPaymentType} {transactions.PaymentTypeAction} - For {Convert.ToDateTime(transactions.TransDate).ToString("MMM dd yyyy")}");
                    cmd.Parameters.AddWithValue("@Category", transactions.Category ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TransNumber", transactions.PaymentTypeReference != null ? $"{transactions.TransType} #{transactions.ItemId} (Ref #{transactions.PaymentTypeReference})" : $"{transactions.TransType} #{transactions.ItemId}");
                    cmd.Parameters.AddWithValue("@TransDate", stringValue);
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
