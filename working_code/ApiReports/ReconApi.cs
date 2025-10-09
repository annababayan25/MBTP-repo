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


namespace MBTP.Services
{

    public class ReconApi : NewbookBaseApi
    {

        private readonly IDatabaseConnectionService _dbConnectionService;

        public ReconApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task PopulateRecons(DateTime startDate, DateTime endDate)
        {

            var body = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
            };

            var json = await PostAsync("reports_reconciliation", body);

            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var reconReport = new List<Recon>();

            foreach (var item in result.data)
            {
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
                    ConcessionalSalesAccomTotal_TaxInc = 0,
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

            using var sqlConn = _dbConnectionService.CreateConnection();
            await sqlConn.OpenAsync();

            
            foreach (var recon in reconReport)
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
            Console.WriteLine("Total Reconciliations: " + reconReport.Count);
            Console.WriteLine("Run method finished.");
        }
    }
}