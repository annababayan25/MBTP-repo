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
using System.Net;
using System.Text.RegularExpressions;


namespace MBTP.Services {

    public class ReconApi 
    {
        private readonly string reconApiUrl = "https://api.newbook.cloud/rest/reports_reconciliation";
        private readonly string transactionFlowApiUrl = "https://api.newbook.cloud/rest/reports_transaction_flow";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly TransactionFlowAPI _transactionApi;

        private readonly IDatabaseConnectionService _dbConnectionService;
        public ReconApi(IDatabaseConnectionService dbConnectionService, TransactionFlowAPI transactionApi)
        {
            _dbConnectionService = dbConnectionService;
            _transactionApi = transactionApi;
        }
        
        public async Task PopulateRecons(DateTime startDate, DateTime endDate) 
        {
            Console.WriteLine("Run method started for reconciliation report");

            var recons = await FetchAllRecons(startDate, endDate);
            if (recons.Count > 0)
            {
                using SqlConnection sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                // Insert bookings
                foreach (var recon in recons)
                {
                    await InsertReconAsync(recon, sqlConn);
                }

                Console.WriteLine("Total Recons: " + recons.Count);
            }
            else
            {
                Console.WriteLine("No recons to display.");
            }

            Console.WriteLine("Run method finished.");
        }

         private async Task InsertReconAsync(Recon recon, SqlConnection sqlConn)
        {
            using (SqlCommand command = new SqlCommand("dbo.UpdateReconReportTable", sqlConn))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@ClientAccount", recon.ClientAccount ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@GLAccount", recon.GLAccount ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Item", recon.ItemType ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Description", recon.ItemDescription ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Date", recon.ItemDate);
                command.Parameters.AddWithValue("@Total_TaxInc", recon.ReconAmount ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Total_Tax", recon.ReconTax ?? (object)DBNull.Value);
                command.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000);
                command.Parameters["@ProcStatus"].Direction = ParameterDirection.Output;
                await command.ExecuteNonQueryAsync();
            }
        }
        

        private async Task<List<Recon>> FetchAllRecons(DateTime startDate, DateTime endDate)
        {
            var periodFrom = startDate.ToString("yyyy-MM-dd HH:mm:ss");
            var periodTo = endDate.ToString("yyyy-MM-dd HH:mm:ss");
            var reconReport = new List<Recon>();

            var requestBody = new
            {
                region = region,
                api_key = apiKey,
                period_from = periodFrom,
                period_to = periodTo
            };

            int loopCount = 0;
            HttpResponseMessage response = new HttpResponseMessage();
            while (loopCount < 5)
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
                var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                response = await httpClient.PostAsync(reconApiUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                    loopCount++;
                    if (loopCount == 5)
                    {
                        return reconReport;
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
                return new List<Recon>();
            }
            var flows = await _transactionApi.FetchAllTransactionsAsync(startDate, endDate);

            foreach (var item in result.data)
            {

                string accountForId = item.account_for_id?.ToString();
                string paymentNumber = ExtractPaymentNumber(item.item_description?.ToString() ?? "");

                var matchingFlows = new List<TransactionFlow>();

                if (!string.IsNullOrEmpty(accountForId))
                {
                    matchingFlows = flows.Where(f => f.AccountForId == accountForId).ToList();
                }

                if (!matchingFlows.Any() && !string.IsNullOrEmpty(paymentNumber))
                {
                    matchingFlows = flows
                        .Where(f => f.PaymentTypeReference == paymentNumber || f.ItemId == paymentNumber)
                        .ToList();
                }

                var clientAccounts = matchingFlows
                    .Select(f => f.ClientAccount)
                    .Where(ca => !string.IsNullOrEmpty(ca))
                    .Distinct()
                    .ToList();

                var clientAccount = clientAccounts.Count > 0
                    ? string.Join(", ", clientAccounts)
                    : item.client_account?.ToString();


                Console.WriteLine("--------------- RECON ---------------");
                Console.WriteLine($"Client Account    : {clientAccount}");
                Console.WriteLine($"GL Account ID     : {item.gl_account_id}");
                Console.WriteLine($"Item Description  : {item.item_description}");
                Console.WriteLine($"Item Date         : {item.item_date}");
                Console.WriteLine($"Reconciled Amount : {item.reconciled_amount}");
                Console.WriteLine($"Reconciled Tax    : {item.reconciled_tax}");
                Console.WriteLine("-------------------------------------\n");

                var recons = new Recon
                {
                    GLAccount = item.gl_account_code,
                    ClientAccount = clientAccount, 
                    ItemType = item.item_type,
                    ItemDescription = item.item_description,
                    ItemDate = item.item_date,
                    ReconAmount = item.reconciled_amount,
                    ReconTax = item.reconciled_tax,
                    TransactionFlows = matchingFlows 
                };


            reconReport.Add(recons);
            }
            return reconReport;

    }
    private string? ExtractPaymentNumber(string description)
    {
        var match = Regex.Match(description ?? "", @"Payment\s+#(\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }


    }
}
