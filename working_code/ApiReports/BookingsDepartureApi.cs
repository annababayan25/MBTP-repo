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
using Google.Protobuf.Reflection;

namespace MBTP.Services
{
    public class BookingsDepartureApi : NewbookBaseApi
    {

        private readonly IDatabaseConnectionService _dbConnectionService;
        public BookingsDepartureApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task PopulateBookingsDeparting(DateTime startDate, DateTime endDate)
        {
            var body = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                list_type = "departing",
                mode = "projected"
            };

            var json = await PostAsync("bookings_list", body);
            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var bookingsList = new List<Booking>();

            int cabin = 0, wesc = 0, travelTrailerMid = 0, travelTrailerSouth = 0, cottage = 0, villa = 0;

            foreach (var item in result.data)
            {

                var bookingsDeparting = new Booking
                {
                    BookingArrival = item.booking_arrival,
                    BookingDeparture = item.booking_departure,
                    BookingCheckedIn = item.booking_checkedin,
                    BookingStatus = item.booking_status,
                    BookingID = item.booking_id,
                    CategoryName = item.category_name
                };

                DateTime bookingDepartureDate = DateTime.Parse(item.booking_departure.ToString()).Date;

                string category = bookingsDeparting.CategoryName;

                string[] excludedCategories = { "Mobile", "Storage", "Parking", "Annual Lease", "Golf Cart" };

                bool isExcluded = excludedCategories.Any(x =>
                category.Contains(x, StringComparison.OrdinalIgnoreCase));

                if (!isExcluded && bookingDepartureDate >= startDate.Date && bookingDepartureDate <= endDate.Date)
                {
                    if (category.Contains("Cabin", StringComparison.OrdinalIgnoreCase)) cabin++;
                    if (category.Contains("WESC", StringComparison.OrdinalIgnoreCase)) wesc++;
                    if (category.Contains("Travel Trailer - Mid Beach", StringComparison.OrdinalIgnoreCase)) travelTrailerMid++;
                    if (category.Contains("Travel Trailer - South Beach", StringComparison.OrdinalIgnoreCase)) travelTrailerSouth++;
                    if (category.Contains("Cottage Rental", StringComparison.OrdinalIgnoreCase)) cottage++;
                    if (category.Contains("Ocean Villa", StringComparison.OrdinalIgnoreCase)) villa++;
                    bookingsList.Add(bookingsDeparting);
                }
            }
            
            var jsonOutput = JsonConvert.SerializeObject(bookingsList, Formatting.Indented);
            var jsonFile = "departingList.json";
            File.WriteAllText(jsonFile, jsonOutput);
            
            Console.WriteLine($"Cabin: {cabin}");
            Console.WriteLine($"WESC: {wesc}");
            Console.WriteLine($"Travel Trailer Mid: {travelTrailerMid}");
            Console.WriteLine($"Travel Trailer South: {travelTrailerSouth}");
            Console.WriteLine($"Cottage: {cottage}");
            Console.WriteLine($"Ocean Villa: {villa}");

            using var sqlConn = _dbConnectionService.CreateConnection();
            await sqlConn.OpenAsync();

            foreach (var bookingsDeparting in bookingsList)
            {
                using (SqlCommand command = new SqlCommand("dbo.UpdateBookingsDepartingTable", sqlConn))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BookingDeparture", bookingsDeparting.BookingDeparture);
                    command.Parameters.AddWithValue("@BookingId", bookingsDeparting.BookingID);
                    command.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;
                    await command.ExecuteNonQueryAsync();
                }
            }
            Console.WriteLine("Total Bookings Departing: " + bookingsList.Count);
            Console.WriteLine("Run method finished.");
        }

    }
}
