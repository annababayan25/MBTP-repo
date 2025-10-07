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
            };

            var json = await PostAsync("bookings_list", body);
            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var bookingsList = new List<Booking>();

            foreach (var item in result.data)
            {

                var bookingsDeparting = new Booking
                {
                    BookingDeparture = item.booking_departure,
                    BookingID = item.booking_id,
                    CategoryName = item.category_name
                };

                DateTime bookingDepartureDate = DateTime.Parse(item.booking_departure.ToString());

                string category = bookingsDeparting.CategoryName;

                if (!(category.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Front Parking Lot", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Annual Lease", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Golf Cart Rental", StringComparison.OrdinalIgnoreCase))
                && (bookingDepartureDate.Date >= startDate.Date && bookingDepartureDate.Date <= endDate.Date))
                {
                    bookingsList.Add(bookingsDeparting);
                    string filePath = "output.txt";
                    File.AppendAllText(filePath, item.ToString() + Environment.NewLine);
                }

            }

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

        public async Task PopulateBookingsDeparted(DateTime startDate, DateTime endDate)
        {
            var body = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                list_type = "departed",
            };

            var json = await PostAsync("bookings_list", body);
            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var bookingsList = new List<Booking>();

            foreach (var item in result.data)
            {

                var bookingsDeparting = new Booking
                {
                    BookingDeparture = item.booking_departure,
                    BookingID = item.booking_id,
                    CategoryName = item.category_name
                };

                DateTime bookingDepartureDate = DateTime.Parse(item.booking_departure.ToString());

                string category = bookingsDeparting.CategoryName;

                if (!(category.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Front Parking Lot", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Annual Lease", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Golf Cart Rental", StringComparison.OrdinalIgnoreCase)))
                {
                    bookingsList.Add(bookingsDeparting);
                    string filePath = "output.txt";
                    File.AppendAllText(filePath, item.ToString() + Environment.NewLine);

                }

                /*
                    if ((bookingDepartureDate.Date >= startDate.Date && bookingDepartureDate.Date <= endDate.Date) && !bookingsDeparting.CategoryName.Contains("Mobile", StringComparison.OrdinalIgnoreCase)
                    && !bookingsDeparting.CategoryName.Contains("Storage - Travel Trailer", StringComparison.OrdinalIgnoreCase) && !bookingsDeparting.CategoryName.Contains("Annual Lease", StringComparison.OrdinalIgnoreCase)
                    && !bookingsDeparting.CategoryName.Contains("Front Parking Lot", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Full Json: " + item.ToString());
                        bookingsList.Add(bookingsDeparting);
                    }*/

            }

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
            Console.WriteLine("Total Bookings Departed: " + bookingsList.Count);
            Console.WriteLine("Run method finished.");
        }

    }
}
