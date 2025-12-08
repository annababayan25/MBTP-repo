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

        public OccupancyApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
        }

        // For Bookings Table in DB
        public async Task<List<OccReport>> PopulateOccupancy(DateTime startDate, DateTime endDate)
        {
            var requestBody = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd"),
                period_to = endDate.ToString("yyyy-MM-dd")
            };

            var json = await PostAsync("reports_occupancy", requestBody);
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
                    ProjEarnings_PerBooking_TaxInc = 0,
                    ProjEarnings_PerBooking_TaxExc = 0,
                    ProjEarnings_PerOccNight_TaxInc = 0,
                    ProjEarnings_PerOccNight_TaxExc = 0,
                    Occupancy = JsonConvert.DeserializeObject<Dictionary<string, OccDetails>>(item.occupancy?.ToString() ?? "{}")
                };

                if (!occupancy.CategoryName.Contains("Beach Pull Thru Site- Concrete Pad - WESC") && !occupancy.CategoryName.Contains("FRONT PARKING LOT")
                && !occupancy.CategoryName.Contains("Employee Site") && !occupancy.CategoryName.Contains("Mobile Home Lease") && !occupancy.CategoryName.Contains("Storage-misc(boats/utility Trailers Etc)")
                && !occupancy.CategoryName.Contains("Storage") && !occupancy.CategoryName.Contains("Golf Cart Rental - ADA only"))
                {
                    occupancyList.Add(occupancy);
                }
            }
            
            return occupancyList;
        }

    }
}