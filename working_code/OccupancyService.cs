using System;
using System.Net.Http;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using MBTP.Models;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MBTP.Services
{
    public class OccupancyService
    {   private readonly IConfiguration _configuration;
        public OccupancyService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<DataSet> GetOccupancyReportAsync(DateTime month)
        {
            DataSet occupancyDataSet = new DataSet();
            try
            {
                Console.WriteLine("Starting GetOccupancyReportAsync method.");

                var periodFrom = new DateTime(month.Year, month.Month, 1);
                var periodTo = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month), 23, 59, 59);
                using (SqlConnection sqlConn = new(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new("dbo.RetrieveDailyOccupancy", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@startDate", SqlDbType.Date).Value = periodFrom;
                    cmd.Parameters.Add("@EndDate", SqlDbType.Date).Value = periodTo;

                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    sqlDataAdapter.Fill(occupancyDataSet);

                    sqlConn.Close();
                }
                return occupancyDataSet;

            }
            catch (Exception ex)
            {
                // Log the exception (consider using a logging framework)
                Console.WriteLine($"Error in GetOccupancyReportAsync: {ex.Message}");
                return new DataSet();
            }
        }
    }
}