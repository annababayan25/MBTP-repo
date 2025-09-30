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
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly IDatabaseConnectionService _dbConnectionService;

        public ReconApi(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
        
        public async Task PopulateRecons(DateTime startDate, DateTime endDate) 
        {
            Console.WriteLine("Run method started for reconciliation report");

            var recons = await FetchAllRecons(startDate, endDate);
            // var charges = await _chargesApi.FetchAllCharges(startDate, endDate);
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
                command.Parameters.AddWithValue("@GLAccount", recon.GLAccountCode);
                command.Parameters.AddWithValue("@ClientAccount", $"(Booking #{recon.BookingId}) {recon.AccountForName}");
                command.Parameters.AddWithValue("@Item", recon.ItemDescription ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Description", recon.GLAccountDescr ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Date", recon.ItemDate);
                command.Parameters.AddWithValue("@Total_TaxInc", recon.Total_TaxInc ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Total_TaxEx", recon.Total_TaxEx ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Total_Tax", recon.Total_Tax ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@TaxFreeTotal_TaxEx", recon.TaxFreeTotal_TaxEx ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@FullSalesAccomTotal_TaxInc", recon.FullSalesAccomTotal_TaxInc ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@FullSalesAccomTotal_Tax", recon.FullSalesAccomTotal_Tax ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ConcessionalSalesAccomTotal_TaxInc", recon.ConcessionalSalesAccomTotal_TaxInc ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ConcessionalSalesAccomTotal_Tax", recon.ConcessionalSalesAccomTotal_Tax ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@PreparedFoodTotal_TaxInc", recon.PreparedFoodTotal_TaxInc ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@PreparedFoodTotal_Tax", recon.PreparedFoodTotal_Tax ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@GolfCartRentalTotal_TaxInc", recon.GolfCartRentalTotal_TaxInc ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@GolfCartRentalTotal_Tax", recon.GolfCartRentalTotal_Tax ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@GolfCartTaxTotal_TaxInc", recon.GolfCartTaxTotal_TaxInc ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@GolfCartTaxTotal_Tax", recon.GolfCartTaxTotal_Tax ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@AdmissionsTotal_TaxInc", recon.AdmissionsTotal_TaxInc ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@AdmissionsTotal_Tax", recon.AdmissionsTotal_Tax ?? (object)DBNull.Value);
                command.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;
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

            foreach (var item in result.data)
            {
                /*
                Console.WriteLine("--------------- RECON ---------------");
                Console.WriteLine($"Client Account    : (Booking #{item.booking_id}) {item.account_for_name}");
                Console.WriteLine($"GL Account Code     : {item.gl_account_code}");
                Console.WriteLine($"Item Description  : {item.item_description}");
                Console.WriteLine($"Item Date         : {item.item_date}");
                Console.WriteLine($"Reconciled Amount : {item.reconciled_amount}");
                Console.WriteLine($"Reconciled Tax    : {item.reconciled_tax}");
                Console.WriteLine("-------------------------------------\n");
                */
                // Console.WriteLine($"Full Recon JSON: {item.ToString()}");

                var recon = new Recon
                {
                    BookingId = item.booking_id,
                    AccountForName = item.account_for_name,
                    AccountForId = item.account_for_id,
                    GLAccountId = item.gl_account_id,
                    GLAccountCode = item.gl_account_code,
                    ItemDescription = item.item_description,
                    GLAccountDescr = item.gl_account_description,
                    ItemDate = item.item_date,
                    Total_TaxInc = item.reconciled_amount,
                    Total_Tax = item.reconciled_tax,
                    Total_TaxEx = 0,
                    FullSalesAccomTotal_TaxInc = 0,
                    FullSalesAccomTotal_Tax = 0,
                    PreparedFoodTotal_TaxInc = 0,
                    PreparedFoodTotal_Tax = 0,
                    ConcessionalSalesAccomTotal_TaxInc =0,
                    ConcessionalSalesAccomTotal_Tax = 0,
                    GolfCartRentalTotal_TaxInc = 0,
                    GolfCartRentalTotal_Tax = 0,
                    GolfCartTaxTotal_TaxInc = 0,
                    GolfCartTaxTotal_Tax = 0,
                    AdmissionsTotal_TaxInc = 0,
                    AdmissionsTotal_Tax = 0
                };

                // calculate total (tax excluded)
                recon.Total_TaxEx = recon.Total_TaxInc - recon.Total_Tax;

                // calculate tax sub-groups
                HashSet<int> accommodation = new HashSet<int> { 1, 5, 6, 7, 16, 17, 18, 19, 37, 39, 48 };
                HashSet<int> preparedFood = new HashSet<int> { 31 };
                HashSet<int> admissions = new HashSet<int> { 38 };
                HashSet<int> golfCart = new HashSet<int> { 46 };

                if (int.TryParse(recon.GLAccountId, out int glIdAcc) && accommodation.Contains(glIdAcc) && recon.Total_Tax != 0)
                {
                    recon.FullSalesAccomTotal_TaxInc = recon.Total_Tax;
                    recon.FullSalesAccomTotal_Tax = recon.Total_Tax;
                }
                
                if (int.TryParse(recon.GLAccountId, out int glIdPrepF) && preparedFood.Contains(glIdPrepF) && recon.Total_Tax != 0)
                {
                    recon.PreparedFoodTotal_TaxInc = recon.Total_Tax;
                    recon.PreparedFoodTotal_Tax = recon.Total_Tax;
                }

                if (int.TryParse(recon.GLAccountId, out int glIdAdm) && admissions.Contains(glIdAdm) && recon.Total_Tax != 0)
                {
                    recon.AdmissionsTotal_TaxInc = recon.Total_Tax;
                    recon.AdmissionsTotal_Tax = recon.Total_Tax;
                }   

                if (int.TryParse(recon.GLAccountId, out int glIdGolf) && golfCart.Contains(glIdGolf) && recon.Total_Tax != 0)
                {
                    recon.GolfCartTaxTotal_TaxInc = recon.Total_Tax;
                    recon.GolfCartTaxTotal_Tax = recon.Total_Tax;
                }   

                // everything else 
                

                if (recon.Total_TaxEx == 0 && recon.Total_Tax == 0)
                {
                    recon.TaxFreeTotal_TaxEx = 0;
                }
                else if (recon.Total_TaxEx != 0 && recon.Total_Tax == 0)
                {
                    recon.TaxFreeTotal_TaxEx = recon.Total_TaxInc;
                }
                else if (recon.Total_TaxEx + recon.Total_Tax == recon.Total_TaxInc)
                {
                    recon.TaxFreeTotal_TaxEx = 0;
                }
                



            reconReport.Add(recon);
        }
        return reconReport;
    }
}

}