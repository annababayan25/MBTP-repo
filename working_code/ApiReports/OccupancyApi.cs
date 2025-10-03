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
using System.IO;

namespace MBTP.Services
{
    public class OccupancyApi : NewbookBaseApi
    {

        private readonly IDatabaseConnectionService _dbConnectionService;

        public OccupancyApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }

        // For Bookings Table in DB
        public async Task PopulateOccupancy(DateTime startDate, DateTime endDate)
        {

            var body = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd"),
                period_to = endDate.ToString("yyyy-MM-dd")
            };

            var json = await PostAsync("reports_occupancy", body);
            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var occupancyList = new List<OccReport>();

            foreach (var item in result.data)
            {
                var occupancy = new OccReport
                {
                    CategoryName = item.category_name,
                    Sites = 0,
                    Available = item.available,
                    Occupied = item.occupied,
                    Maintenance = item.maintenance,
                    Allotted = item.allotted,
                    RevenueGross = item.revenue_gross,
                    RevenueNet = item.revenue_net,
                    Occupancy = JsonConvert.DeserializeObject<Dictionary<string, OccDetails>>(item.occupancy?.ToString() ?? "{}")
                };

                occupancyList.Add(occupancy);
            }


            using var sqlConn = _dbConnectionService.CreateConnection();
            await sqlConn.OpenAsync();

            foreach (var occupancy in occupancyList)
            {
                foreach (var kvp in occupancy.Occupancy)
                {
                    var occDate = kvp.Key;
                    var occDetails = kvp.Value;

                    using (SqlCommand command = new SqlCommand("dbo.UpdateOccupancyTable", sqlConn))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.AddWithValue("@Category", occupancy.CategoryName);
                        command.Parameters.AddWithValue("@OccupancyDate", occDate ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Sites",  occDetails.Available ?? 0);
                        command.Parameters.AddWithValue("@NightsAvailable", occDetails.Available ?? 0);
                        command.Parameters.AddWithValue("@Bookings", occDetails.Occupied ?? 0);
                        command.Parameters.AddWithValue("@BookingLength", $"{occDetails.Occupied} Nights");
                        command.Parameters.AddWithValue("@ProjEarnings_TaxInc", occDetails.RevenueGross ?? 0);
                        command.Parameters.AddWithValue("@ProjEarnings_TaxExc", occDetails.RevenueNet ?? 0);
                        command.Parameters.AddWithValue("@ProjEarnings_PerBooking_TaxInc", 0);
                        command.Parameters.AddWithValue("@ProjEarnings_PerBooking_TaxExc", 0);
                        command.Parameters.AddWithValue("@ProjEarnings_PerOccNight_TaxInc", 0);
                        command.Parameters.AddWithValue("@ProjEarnings_PerOccNight_TaxExc", 0);
                        command.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000);
                        command.Parameters["@ProcStatus"].Direction = ParameterDirection.Output;

                        await command.ExecuteNonQueryAsync();

                    }
                }
            }
            Console.WriteLine("Total Occupancy Entries: " + occupancyList.Count);
            Console.WriteLine("Run method finished.");
        }

    }
}