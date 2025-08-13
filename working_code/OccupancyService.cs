using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MBTP.Interfaces;

namespace MBTP.Services
{
    public class OccupancyService
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public OccupancyService(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task<DataSet> GetOccupancyReportAsync(DateTime month)
        {
            DataSet occupancyDataSet = new DataSet();

            try
            {
                Console.WriteLine("Starting GetOccupancyReportAsync method.");

                var periodFrom = new DateTime(month.Year, month.Month, 1);
                var periodTo = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month), 23, 59, 59);

                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand("dbo.RetrieveDailyOccupancy", sqlConn))
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
                Console.WriteLine($"Error in GetOccupancyReportAsync: {ex.Message}");
                return new DataSet();
            }
        }
    }
}
