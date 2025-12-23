
using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Services
{
    public class ReconRepo
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public ReconRepo(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task SaveReconAsync(IEnumerable<ReconsApi> reconReport)
        {
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
            Console.WriteLine($"Total Reconciliations: {reconReport.Count()}");
            Console.WriteLine("Run method finished.");
        }
    }
}