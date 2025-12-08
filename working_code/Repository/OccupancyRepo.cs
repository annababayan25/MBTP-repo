
using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Services
{
    public class OccupancyRepo
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public OccupancyRepo(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task SaveOccupancyAsync(IEnumerable<OccReport> occupancyList, DateTime startDate)
        {
            using var sqlConn = _dbConnectionService.CreateConnection();
            await sqlConn.OpenAsync();

            foreach (var occupancy in occupancyList)
            {
                var reportDate = startDate.ToString("yyyy-MM-dd");
                if (occupancy.Occupancy.TryGetValue(reportDate, out var occDetails))
                {
                    var occDate = reportDate;

                    decimal projPerBookingTaxInc = 0;
                    decimal projPerBookingTaxExc = 0;
                    decimal projPerOccNightTaxInc = 0;
                    decimal projPerOccNightTaxExc = 0;

                    if ((occDetails.Occupied ?? 0) > 0)
                    {
                        projPerBookingTaxInc = ((decimal)(occDetails.RevenueGross ?? 0)) / (occDetails.Occupied ?? 0);
                        projPerBookingTaxExc = ((decimal)(occDetails.RevenueNet ?? 0)) / (occDetails.Occupied ?? 0);
                        projPerOccNightTaxInc = ((decimal)(occDetails.RevenueGross ?? 0)) / (occDetails.Occupied ?? 0);
                        projPerOccNightTaxExc = ((decimal)(occDetails.RevenueNet ?? 0)) / (occDetails.Occupied ?? 0);
                    }

                    using (SqlCommand command = new SqlCommand("dbo.UpdateOccupancyTable", sqlConn))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.AddWithValue("@Category", occupancy.CategoryName);
                        command.Parameters.AddWithValue("@OccupancyDate", occDate ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Sites", occDetails.Available ?? 0);
                        command.Parameters.AddWithValue("@NightsAvailable", occDetails.Available ?? 0);
                        command.Parameters.AddWithValue("@Bookings", occDetails.Occupied ?? 0);
                        command.Parameters.AddWithValue("@BookingLength", $"{occDetails.Occupied} Nights");
                        command.Parameters.AddWithValue("@ProjEarnings_TaxInc", occDetails.RevenueGross ?? 0);
                        command.Parameters.AddWithValue("@ProjEarnings_TaxExc", occDetails.RevenueNet ?? 0);
                        command.Parameters.AddWithValue("@ProjEarnings_PerBooking_TaxInc", projPerBookingTaxInc);
                        command.Parameters.AddWithValue("@ProjEarnings_PerBooking_TaxExc", projPerBookingTaxExc);
                        command.Parameters.AddWithValue("@ProjEarnings_PerOccNight_TaxInc", projPerOccNightTaxInc);
                        command.Parameters.AddWithValue("@ProjEarnings_PerOccNight_TaxExc", projPerOccNightTaxExc);
                        command.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000);
                        command.Parameters["@ProcStatus"].Direction = ParameterDirection.Output;

                        await command.ExecuteNonQueryAsync();

                    }
                }
            }
            Console.WriteLine($"Total Occupancy Entries: {occupancyList.Count()}");
            Console.WriteLine("Run method finished.");
        }
    }

}