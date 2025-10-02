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
    public class BookingsStayingApi
    {
        private readonly string apiUrl = "https://api.newbook.cloud/rest/bookings_groups_list";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly IDatabaseConnectionService _dbConnectionService;
        public BookingsStayingApi(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
        
        // For Bookings Table in DB
        public async Task PopulateBookingsStaying(DateTime startDate, DateTime endDate)
        {
            Console.WriteLine("Run method started.");

            var bookings = await FetchAllBookingsStayingAsync(startDate, endDate);

            if (bookings.Count > 0)
            {
                using SqlConnection sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                // Insert bookings
                foreach (var booking in bookings)
                {
                    await InsertBookingStayingAsync(booking, sqlConn);
                }

                Console.WriteLine("Total Bookings: " + bookings.Count);
            }
            else
            {
                Console.WriteLine("No bookings to display.");
            }

            Console.WriteLine("Run method finished.");
        }

        private async Task InsertBookingStayingAsync(BookingsStaying bookingsStaying, SqlConnection sqlConn)
        {
            using (SqlCommand command = new SqlCommand("dbo.UpdateBookingsStayingTable", sqlConn))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@Category", bookingsStaying.Category);
                command.Parameters.AddWithValue("@BookingGroupId", bookingsStaying.BookingGroupId);
                command.Parameters.AddWithValue("@BookingGroupName", bookingsStaying.BookingGroupName);
                command.Parameters.Add("@status", SqlDbType.NVarChar, 4000);
                command.Parameters["@status"].Direction = ParameterDirection.Output;
                await command.ExecuteNonQueryAsync();
                //Console.WriteLine(command.Parameters["@status"].Value.ToString());
            }
        }

        private async Task<List<BookingsStaying>> FetchAllBookingsStayingAsync(DateTime startDate, DateTime endDate)
        {
            var periodFrom = startDate.ToString("yyyy-MM-dd");
            var periodTo = endDate.ToString("yyyy-MM-dd");
            var bookingsList = new List<BookingsStaying>();
            
                var requestBody = new
                {
                    region = region,
                    api_key = apiKey,
                    period_from = periodFrom,
                    period_to = periodTo,
                    list_type = "inhouse",
                    restrict_mail_outs = 1
                };

                int loopCount = 0;
                HttpResponseMessage response = new HttpResponseMessage();
                while (loopCount < 5)
                {
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
                    var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                    var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                    response = await httpClient.PostAsync(apiUrl, content);
                    
                    if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                    loopCount++;
                    if (loopCount == 5)
                    {
                        return bookingsList;
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
                    return new List<BookingsStaying>();
                }


            foreach (var item in result.data)
            {

                var bookingsStaying = new BookingsStaying
                {
                    BookingGroupId = item.bookings_group_id,
                    BookingGroupName = item.bookings_group_name,
                    Bookings = JsonConvert.DeserializeObject<List<Bookings>>(item.bookings?.ToString() ?? "[]")
                };

                Console.WriteLine("Full JSon: " + item.ToString());
                    
                    
                    bookingsList.Add(bookingsStaying);
            }
        
            return bookingsList;
        }

    }
}

