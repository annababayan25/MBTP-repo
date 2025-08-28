using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Services
{
    public class DailyService
    {
        private readonly string apiUrl = "https://api.newbook.cloud/rest/bookings_list";
        private readonly string region = "us";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a"; // Replace with your actual API key
        private readonly string username = "myrtle_beach"; // Replace with your actual username
        private readonly string password = "Gemb$np(QqEnB9V3"; // Replace with your actual password

        public async Task<DailyBookingReport> GetBookingsAsync(DateTime periodFrom, DateTime periodTo)
        {
            try
            {
                Console.WriteLine("Starting GetBookingsAsync method.");
                var request = new
                {
                    region = region,
                    api_key = apiKey,
                    period_from = periodFrom.ToString("yyyy-MM-dd HH:mm:ss"),
                    period_to = periodTo.ToString("yyyy-MM-dd HH:mm:ss"),
                    list_type = "placed"
                };
                Console.WriteLine($"Requesting Placed data from {apiUrl} for period from {request.period_from} to {request.period_to}.");
                var jsonResponse = await PostDataAsync(apiUrl, request);

                if (jsonResponse != null)
                {
                    Console.WriteLine("Placed Data retrieved successfully from API.");
                    var bookingsResponse = JsonConvert.DeserializeObject<BookingsResponseP>(jsonResponse);
                    Console.WriteLine($"Total items in API response: {bookingsResponse?.Data?.Count}");
                    var PlacedBookingsByDay = bookingsResponse.Data
                        .GroupBy(b => b.BookingPlaced.Date)
                        .ToDictionary(g => g.Key, g => g.Count());
                    List<DailyBookingP> DailyPlacedList = new List<DailyBookingP>(PlacedBookingsByDay.Keys.Count);
                    foreach (var item in PlacedBookingsByDay)
                    {
                        DailyPlacedList.Add(new DailyBookingP { Day = item.Key, Placed = item.Value });
                    }

                    request = new
                    {
                        region = region,
                        api_key = apiKey,
                        period_from = periodFrom.ToString("yyyy-MM-dd HH:mm:ss"),
                        period_to = periodTo.ToString("yyyy-MM-dd HH:mm:ss"),
                        list_type = "cancelled"
                    };

                    Console.WriteLine($"Requesting Cancelled data from {apiUrl} for period from {request.period_from} to {request.period_to}.");
                    jsonResponse = await PostDataAsync(apiUrl, request);

                    if (jsonResponse != null)
                    {
                        Console.WriteLine("Cancelled Data retrieved successfully from API.");
                        var bookingsResponse2 = JsonConvert.DeserializeObject<BookingsResponseC>(jsonResponse);
                        Console.WriteLine($"Total items in API response: {bookingsResponse2?.Data?.Count}");
                        var CancelledBookingsByDay = bookingsResponse2.Data
                            .GroupBy(b2 => b2.BookingCancelled.Date)
                            .ToDictionary(g => g.Key, g => g.Count());
                        List<DailyBookingC> DailyCancelledList = new List<DailyBookingC>(CancelledBookingsByDay.Keys.Count);
                        foreach (var item in CancelledBookingsByDay)
                        {
                            DailyCancelledList.Add(new DailyBookingC { Day = item.Key, Cancelled = item.Value });
                        }
                        return new DailyBookingReport
                        {
                            Month = periodFrom,
                            DailyPlaced = DailyPlacedList,
                            DailyCancelled = DailyCancelledList
                        };
                    }
                }

                throw new Exception("Failed to retrieve data from API.");
            }
            catch (Exception ex)
            {
                // Log the exception (consider using a logging framework)
                Console.WriteLine($"Error in GetBookingsAsync: {ex.Message}");
                return new DailyBookingReport();
            }
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

        public class BookingsResponseP
        {
            [JsonProperty("data")]
            public List<BookingP> Data { get; set; }
        }

        public class BookingP
        {
            [JsonProperty("booking_placed")]
            public DateTime BookingPlaced { get; set; }
            // Add other relevant properties here
        }
        public class BookingsResponseC
        {
            [JsonProperty("data")]
            public List<BookingC> Data { get; set; }
        }

        public class BookingC
        {
            [JsonProperty("booking_placed")]
            public DateTime BookingCancelled { get; set; }
            // Add other relevant properties here
        }
    }
}