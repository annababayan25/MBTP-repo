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
    public class BookingsStayingApi : NewbookBaseApi
    {

        private readonly IDatabaseConnectionService _dbConnectionService;
        public BookingsStayingApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }

        // For Bookings Table in DB
        public async Task PopulateBookingsStaying(DateTime startDate, DateTime endDate)
        {

            var body = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd"),
                period_to = endDate.ToString("yyyy-MM-dd"),
                list_type = "inhouse",
                restrict_mail_outs = 1
            };

            var json = await PostAsync("bookings_groups_list", body);
            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var bookingsList = new List<BookingsStaying>();

            foreach (var item in result.data)
            {
                var bookingsStaying = new BookingsStaying
                {
                    BookingGroupId = item.bookings_group_id,
                    BookingGroupName = item.bookings_group_name,
                    Bookings = JsonConvert.DeserializeObject<List<Bookings>>(item.bookings?.ToString() ?? "[]")
                };

                Console.WriteLine("Full Json: " + item.ToString());


                bookingsList.Add(bookingsStaying);
            }

            using var sqlConn = _dbConnectionService.CreateConnection();
            await sqlConn.OpenAsync();

            foreach (var bookingsStaying in bookingsList)
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
                }
            }
            Console.WriteLine("Total Bookings Staying: " + bookingsList.Count);
            Console.WriteLine("Run method finished.");
        }

    }
}

       