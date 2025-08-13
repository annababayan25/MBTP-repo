using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using MBTP.Models;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MBTP.Services
{
    public class OccupancyServiceOLD
    {
        private readonly string apiUrl = "https://api.newbook.cloud/rest/reports_occupancy";
        private readonly string apiUrl2 = "https://api.newbook.cloud/rest/bookings_list";
        private readonly string region = "us";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly List<string> allowedCategoryNames = new List<string>
        {
            "OceanFront WESC",
            "Beach - Pull Thru - WESC",
            "LakeFront - Beachside WESC",
            "Beach - Back In Site - WESC",
            "LakeFront - Shaded area WESC",
            "Shaded - Back In - WESC",
            "Water & Electric Only",
            "Cottage Rental",
            "Ocean Villa",
            "Travel Trailer - South Beach",
            "Travel Trailer - Mid Beach",
            "Cabin"
         };
        private readonly List<string> disallowedBookingSources = new List<string>
        {
            "gc",
            "gc - test",
            "kk"
         };

  public async Task<FilteredOccupancyReport> GetOccupancyReportAsync(DateTime month, bool IncludeAges)
        {
            try
            {
                Console.WriteLine("Starting GetOccupancyReportAsync method.");

                var periodFrom = new DateTime(month.Year, month.Month, 1);
                var periodTo = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month), 23, 59, 59);
                var periodFromStr = periodFrom.ToString("yyyy-MM-dd HH:mm:ss");
                var periodToStr = periodTo.ToString("yyyy-MM-dd HH:mm:ss");
                var staying = "staying";
                var request = new
                {
                    region = region,
                    api_key = apiKey,
                    period_from = periodFromStr,
                    period_to = periodToStr
                };

                Console.WriteLine($"Requesting data from {apiUrl} for period from {periodFromStr} to {periodToStr}.");

                var jsonResponse = await PostDataAsync(apiUrl, request);

                if (jsonResponse != null)
                {
                    Console.WriteLine("Data retrieved successfully from API.");
                    var occupancyReport = JsonConvert.DeserializeObject<OccupancyReport>(jsonResponse);
                    Console.WriteLine($"Total items in API response: {occupancyReport?.Data?.Count}");

                    // Log available category names
                    var allCategories = occupancyReport.Data.Select(d => d.CategoryName).Distinct().ToList();
                    Console.WriteLine("Categories from API:");
                    foreach (var category in allCategories)
                    {
                        Console.WriteLine($"'{category}'");
                    }

                    // Filter the data to include only allowed categories
                    var filteredData = occupancyReport.Data.Where(d => allowedCategoryNames.Contains(d.CategoryName)).ToList();
                    Console.WriteLine($"Total items after filtering: {filteredData.Count}");
                    var dailyOccupancies = CalculateDailyOccupancies(filteredData);

                    if (IncludeAges)
                    {
                        // now get the occupant breakdown
                        var request2 = new
                        {
                            region = region,
                            api_key = apiKey,
                            period_from = periodFromStr,
                            period_to = "2025-05-01 23:59:59",
                            list_type = staying
                        };
                        var jsonResponse2 = await PostDataAsync(apiUrl2, request2);
                        if (jsonResponse2 != null)
                        {
                            var occupantReport = JsonConvert.DeserializeObject<OccupantReport>(jsonResponse2);
                            // Filter the data to include only allowed categories
                            var filteredData2 = occupantReport.Data.Where(d => allowedCategoryNames.Contains(d.CategoryName) && !disallowedBookingSources.Contains(d.Bookingsource)).ToList();
                            var dailyOccupants = CalculateDailyOccupants(periodFrom, periodTo, filteredData2);
                            return new FilteredOccupancyReport
                            {
                                Month = month,
                                DailyOccupancies = dailyOccupancies,
                                DailyOccupants = dailyOccupants
                            };
                        }
                    }
                    else
                    {
                        return new FilteredOccupancyReport
                        {
                            Month = month,
                            DailyOccupancies = dailyOccupancies,
                            DailyOccupants = LoadBogusOccupants(periodFrom)
                        };
                    }
                }

                throw new Exception("Failed to retrieve data from API.");
            }
            catch (Exception ex)
            {
                // Log the exception (consider using a logging framework)
                Console.WriteLine($"Error in GetOccupancyReportAsync: {ex.Message}");
                return new FilteredOccupancyReport
                {
                    Month = month,
                    DailyOccupancies = new List<DailyOccupancy>(),
                    ErrorMessage = ex.Message
                };
            }
        }

        private List<DailyOccupancy> CalculateDailyOccupancies(List<OccupancyData> filteredData)
        {
            Console.WriteLine("Starting CalculateDailyOccupancies method.");
            var dailyOccupancies = new Dictionary<DateTime, DailyOccupancy>();

            foreach (var data in filteredData)
            {
                foreach (var occupancy in data.Occupancy.Values)
                {
                    if (!dailyOccupancies.ContainsKey(occupancy.Date))
                    {
                        dailyOccupancies[occupancy.Date] = new DailyOccupancy
                        {
                            Date = occupancy.Date,
                            TotalAvailable = 0,
                            TotalOccupied = 0,
                            OccupancyRate = 0
                        };
                    }

                    dailyOccupancies[occupancy.Date].TotalAvailable += occupancy.Available;
                    dailyOccupancies[occupancy.Date].TotalOccupied += occupancy.Occupied;
                }
            }
            foreach (var dailyOccupancy in dailyOccupancies.Values){
                if (dailyOccupancy.TotalAvailable > 0){
                    dailyOccupancy.OccupancyRate = (double)dailyOccupancy.TotalOccupied / dailyOccupancy.TotalAvailable;
                }
                else{
                    dailyOccupancy.OccupancyRate = 0;
                }
            }

            Console.WriteLine($"Total days with data: {dailyOccupancies.Count}");
            Console.WriteLine("Finished calculating daily occupancies.");
            return dailyOccupancies.Values.OrderBy(d => d.Date).ToList();
        }
        private List<DailyOccupants> CalculateDailyOccupants(DateTime startDate, DateTime endDate, List<OccupantData> filteredData2)
        {
            Console.WriteLine("Starting CalculateDailyOccupants method.");
            var dailyOccupants = new Dictionary<DateTime, DailyOccupants>();
            Console.WriteLine(filteredData2.Count);
            foreach (var data in filteredData2)
            {
                for (DateTime i = data.Bookingarrival.Date; i <= data.Bookingdeparture.Date; i = i.AddDays(1))
                {
                    if (i >= startDate.Date)
                    {
                        if (i > endDate.Date)
                        {
                            break;
                        }
                        if (!dailyOccupants.ContainsKey(i))
                        {
                            dailyOccupants[i] = new DailyOccupants
                            {
                                Date = i,
                                TotalAdults = 0,
                                TotalChildren = 0,
                                TotalInfants = 0
                            };
                        }
                        Console.WriteLine(data.BookingId);
                        dailyOccupants[i].TotalAdults += data.Bookingadults;
                        dailyOccupants[i].TotalChildren += data.Bookingchildren;
                        dailyOccupants[i].TotalInfants += data.Bookinginfants;
                    }
                }
            }
            return dailyOccupants.Values.OrderBy(d => d.Date).ToList();
        }

        private List<DailyOccupants> LoadBogusOccupants(DateTime startDate)
        {
            Console.WriteLine("Starting LoadBogusOccupants method.");
            var dailyOccupants = new Dictionary<DateTime, DailyOccupants>();
            DateTime i = startDate.AddDays(-1);
            dailyOccupants[i] = new DailyOccupants
            {
                Date = i,
                TotalAdults = 0,
                TotalChildren = 0,
                TotalInfants = 0
            };
            return dailyOccupants.Values.OrderBy(d => d.Date).ToList();
        }
        private async Task<string> PostDataAsync(string url, object request)
        {
            using (var client = new HttpClient())
            {
                var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));
                var jsonContent = JsonConvert.SerializeObject(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Console.WriteLine("Sending POST request to API.");
                if (url == apiUrl2)
                {
                    client.Timeout = TimeSpan.FromMinutes(5);
                }
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("API response received successfully.");
                    return await response.Content.ReadAsStringAsync();
                }

                // Log the error response
                var errorMessage = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API Error: {errorMessage}");

                throw new Exception("Failed to retrieve data from API.");
            }
        }
    }
}