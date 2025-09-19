  /*
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

    public class ChargesApi 
    {
        private readonly string apiUrl = "https://api.newbook.cloud/rest/charges_list";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly IDatabaseConnectionService _dbConnectionService;

        public ChargesApi(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
        
      
        public async Task PopulateRecons(DateTime startDate, DateTime endDate) 
        {
            Console.WriteLine("Run method started for reconciliation report");

            
            if (charges.Count > 0)
            {
                using SqlConnection sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                // Insert bookings
                foreach (var charge in charges)
                {
                    await InsertChargesAsync(charge, sqlConn);
                }

                Console.WriteLine("Total Charges: " + charges.Count);
            }
            else
            {
                Console.WriteLine("No charges to display.");
            }
        
            Console.WriteLine("Run method finished.");
        }
       
        
         private async Task InsertReconAsync(Recon recon, SqlConnection sqlConn)
        {
            using (SqlCommand command = new SqlCommand("dbo.UpdateReconReportTable", sqlConn))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@GLAccount", recon.GLAccountCode);
                command.Parameters.AddWithValue("@ClientAccount", $"(Booking #{recon.BookingId}) {recon.AccountForName}");
                command.Parameters.AddWithValue("@Item", recon.ItemDescription ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Description", recon.GLAccountDescr ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Date", recon.ItemDate);
                command.Parameters.AddWithValue("@Total_TaxInc", recon.ReconAmount ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Total_TaxEx", recon.TotalTaxEx ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Total_Tax", recon.ReconTax ?? (object)DBNull.Value);
                command.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000);
                command.Parameters["@ProcStatus"].Direction = ParameterDirection.Output;
                await command.ExecuteNonQueryAsync();
            }
        }


        public async Task<List<Charges>> FetchAllCharges(DateTime startDate, DateTime endDate)
        {
            var periodFrom = startDate.ToString("yyyy-MM-dd HH:mm:ss");
            var periodTo = endDate.ToString("yyyy-MM-dd HH:mm:ss");
            var chargesList = new List<Charges>();

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

                response = await httpClient.PostAsync(apiUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                    loopCount++;
                    if (loopCount == 5)
                    {
                        return chargesList;
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
                return new List<Charges>();
            }

            foreach (var item in result.data)
            {
                
                Console.WriteLine("--------------- RECON ---------------");
                Console.WriteLine($"Client Account    : (Booking #{item.booking_id}) {item.account_for_name}");
                Console.WriteLine($"GL Account Code     : {item.gl_account_code}");
                Console.WriteLine($"Item Description  : {item.item_description}");
                Console.WriteLine($"Item Date         : {item.item_date}");
                Console.WriteLine($"Reconciled Amount : {item.reconciled_amount}");
                Console.WriteLine($"Reconciled Tax    : {item.reconciled_tax}");
                Console.WriteLine("-------------------------------------\n");
                
               
                    if (item.tax_breakdown != null)
                    {
                        foreach (var tb in item.tax_breakdown)
                        {
                            string taxName = (string)tb.tax_name;
                            if (!string.IsNullOrEmpty(taxName) && taxName.Contains("Golf Cart"))
                            {
                                Console.WriteLine($"Full Charges JSON: {item.ToString()}");
                            }
                        }
                    }
                

                var charges = new Charges
                {
                    Id = item.id,
                    AccountForName = item.account_for_name,
                    AccountForId = item.account_for_id,
                    GLAccountCode = item.gl_account_code,
                    Amount = item.amount,
                    AmountIncTax = item.amount_inc_tax,
                    AmountExTax = item.amount_ex_tax,
                    Tax = item.tax,
                    TaxFree = item.tax_free,
                    TaxBreakdown = item.tax_breakdown != null ? JsonConvert.DeserializeObject<List<TaxBreakdown>>(item.tax_breakdown.ToString())
                    : new List<TaxBreakdown>()
                };

            chargesList.Add(charges);
        }
        return chargesList;
    }
}

}
 */