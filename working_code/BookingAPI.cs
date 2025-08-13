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

namespace MBTP.Services
{
    public class NewBookService
    {
        private readonly string apiUrl = "https://api.newbook.cloud/rest/bookings_list";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly IDatabaseConnectionService _dbConnectionService;
        public NewBookService(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

       public async Task PopulateBookings(DateTime startDate, DateTime endDate)
        {
            Console.WriteLine("Run method started.");
            var bookings = await FetchAllBookingsAsync(startDate, endDate);
//            var bookingsBase = await FetchBookingsAsync(startDate, endDate, "all");
//            var bookingsC = await FetchBookingsAsync(startDate, endDate, "cancelled");
//            var bookingsN = await FetchBookingsAsync(startDate, endDate, "no_show");
//            var bookingsA = await FetchBookingsAsync(startDate, endDate, "arrived");
//            var bookingsD = await FetchBookingsAsync(startDate, endDate, "departed");
//            bookings.AddRange(bookingsC);
//            bookings.AddRange(bookingsN);
//            bookings.AddRange(bookingsA);
//            bookings.AddRange(bookingsD);
            if (bookings.Count > 0)
            {
                SqlConnection sqlConn = _dbConnectionService.CreateConnection();
                sqlConn.Open();
                foreach (var booking in bookings)
                {
                    //Console.WriteLine($"Booking ID: {booking.BookingID}, ExpressCheckIn: {booking.ExpressCheckin}");
                    await InsertBookingAsync(booking, sqlConn);
                }
                sqlConn.Close();
                Console.WriteLine("Total Bookings: " + bookings.Count.ToString());
            }
            else
            {
                Console.WriteLine("No bookings to display.");
            }
            Console.WriteLine("Run method finished.");
        }

        private async Task InsertBookingAsync(Booking booking, SqlConnection sqlConn)
        {
            using (SqlCommand command = new SqlCommand("dbo.UpdateBookingsTable", sqlConn))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@BookingID", booking.BookingID);
                command.Parameters.AddWithValue("@SiteName", booking.SiteName);
                command.Parameters.AddWithValue("@BookingArrival", booking.BookingArrival);
                command.Parameters.AddWithValue("@BookingDeparture", booking.BookingDeparture);
                command.Parameters.AddWithValue("@BookingStatus", booking.BookingStatus ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@BookingAdults", booking.BookingAdults);
                command.Parameters.AddWithValue("@BookingChildren", booking.BookingChildren);
                command.Parameters.AddWithValue("@BookingInfants", booking.BookingInfants);
                command.Parameters.AddWithValue("@BookingTotal", booking.BookingTotal);
                command.Parameters.AddWithValue("@BookingMethodName", booking.BookingMethodName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@BookingSourceName", booking.BookingSourceName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@BookingReasonName", booking.BookingReasonName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@AccountBalance", booking.AccountBalance);
                command.Parameters.AddWithValue("@BookingPlaced", booking.BookingPlaced);
                command.Parameters.AddWithValue("@StateName", booking.StateName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CategoryName", booking.CategoryName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@BookingCancelled", booking.BookingCancelled ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ExpressCheckin", booking.ExpressCheckin);
                command.Parameters.AddWithValue("@StoredMBTP", booking.StoredMBTP ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@StoredOutside", booking.StoredOutside ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@EquipmentMake", booking.EquipmentMake ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@EquipmentModel", booking.EquipmentModel ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@EquipmentLength", booking.EquipmentLength ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@FirstName", booking.Firstname ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@LastName", booking.Lastname ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Wristbands", booking.Wristbands);
                command.Parameters.Add("@status", SqlDbType.NVarChar, 4000);
                command.Parameters["@status"].Direction = ParameterDirection.Output;
                await command.ExecuteNonQueryAsync();
                //Console.WriteLine(command.Parameters["@status"].Value.ToString());
            }
        }

        private async Task<List<Booking>> FetchBookingsAsync(DateTime startDate, DateTime endDate, string listType)
        {
            var periodFrom = startDate.ToString("yyyy-MM-dd HH:mm:ss");
            var periodTo = endDate.ToString("yyyy-MM-dd HH:mm:ss");
            var requestBody = new
            {
                region = region,
                api_key = apiKey,
                period_from = periodFrom,
                period_to = periodTo,
                list_type = listType
            };
            
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
            var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            Console.WriteLine("Sending HTTP POST request for " + listType + "...");

            var response = await httpClient.PostAsync(apiUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                return new List<Booking>();
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
            //Console.WriteLine($"HTTP JSON RESPONSE: {jsonResponse}");
            if (result is null || result.success != "true")
            {
                Console.WriteLine("API response indicates failure.");
                return new List<Booking>();
            }

            var bookings = new List<Booking>();
            foreach (var item in result.data)
            {
                var booking = new Booking
                {
                    BookingID = item.booking_id,
                    BookingArrival = item.booking_arrival,
                    BookingDeparture = item.booking_departure,
                    BookingStatus = item.booking_status,
                    BookingAdults = item.booking_adults,
                    BookingChildren = item.booking_children,
                    BookingInfants = item.booking_infants,
                    BookingTotal = item.booking_total,
                    BookingMethodName = item.booking_method_name,
                    BookingSourceName = item.booking_source_name,
                    BookingReasonName = item.booking_reason_name,
                    CategoryName = item.category_name,
                    AccountBalance = item.account_balance,
                    BookingPlaced = item.booking_placed,
                    BookingCancelled = item.booking_cancelled,
                    Guests = JsonConvert.DeserializeObject<List<Guests>>(item.guests.ToString()) // Deserialize the guests list
                };

                // Assign the state property from the first guest in the list (if any)
                if (booking.Guests != null && booking.Guests.Count > 0)
                {
                    booking.StateName = booking.Guests[0].State;
                }
                else
                {
                    booking.StateName = "Unknown";
                }
                bookings.Add(booking);
                if (booking.BookingAdults + booking.BookingChildren + booking.BookingInfants != 0)
                {
                    bookings.Add(booking);
                }
                else
                {
                    Console.WriteLine("Booking ID " + booking.BookingID + " not added");
                }
            }
            return bookings;
        }
        private async Task<List<Booking>> FetchAllBookingsAsync(DateTime startDate, DateTime endDate)
        {
            var periodFrom = startDate.ToString("yyyy-MM-dd HH:mm:ss");
            var periodTo = endDate.ToString("yyyy-MM-dd HH:mm:ss");
            var dataOffset = 0;
            var dataCount = 100;
            var dataTotal = 100000;
            var bookings = new List<Booking>();
            while(dataOffset < dataTotal)
            {
                var requestBody = new
                {
                    region = region,
                    api_key = apiKey,
                    period_from = periodFrom,
                    period_to = periodTo,
                    list_type = "all",
                    data_offset = dataOffset,
                    data_count = dataCount
                };
                int loopCount = 0;
                HttpResponseMessage response = new HttpResponseMessage();
                while (loopCount < 5)
                {
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
                    var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                    var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                    Console.WriteLine("Sending HTTP POST request for data offset " + dataOffset.ToString() + "...");

                    response = await httpClient.PostAsync(apiUrl, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                        loopCount++;
                        if (loopCount == 5)
                        {
                            return bookings;
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
                foreach (var jsonToken in jsonObject.Children<JProperty>())
                {
                    if (jsonToken.Name == "data_total")
                    {
                        dataTotal = (int)jsonToken.Value;
                    }
                    else if (jsonToken.Name == "data_count")
                    {
                        dataOffset += (int)jsonToken.Value;
                    }
                }
                var result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                //Console.WriteLine($"HTTP JSON RESPONSE: {jsonResponse}");
                if (result is null || result.success != "true")
                {
                    Console.WriteLine("API response indicates failure.");
                    return new List<Booking>();
                }

                foreach (var item in result.data)
                {
                    var booking = new Booking
                    {
                        BookingID = item.booking_id,
                        SiteName = item.site_name,
                        BookingArrival = item.booking_arrival,
                        BookingDeparture = item.booking_departure,
                        BookingStatus = item.booking_status,
                        BookingAdults = item.booking_adults,
                        BookingChildren = item.booking_children,
                        BookingInfants = item.booking_infants,
                        BookingTotal = item.booking_total,
                        BookingMethodName = item.booking_method_name,
                        BookingSourceName = item.booking_source_name,
                        BookingReasonName = item.booking_reason_name,
                        CategoryName = item.category_name,
                        AccountBalance = item.account_balance,
                        BookingPlaced = item.booking_placed,
                        BookingCancelled = item.booking_cancelled,
                        ExpressCheckin = item.booking_demographic_name,
                        Guests = JsonConvert.DeserializeObject<List<Guests>>(item.guests.ToString()), // Deserialize the guests list
                        CustomFields = JsonConvert.DeserializeObject<List<CustomFields>>(item.custom_fields.ToString()), // Deserialize the custom fields list
                        Equipment = JsonConvert.DeserializeObject<List<EquipmentFields>>(item.equipment.ToString()) // Deserialize the equipment fields list
                    };
                    // Assign the state property from the first guest in the list (if any)
                    if (booking.Guests != null && booking.Guests.Count > 0)
                    {
                        booking.StateName = booking.Guests[0].State;
                        booking.Firstname = booking.Guests[0].Firstname;
                        booking.Lastname = booking.Guests[0].Lastname;
                    }
                    else
                    {
                        booking.StateName = "Unknown";
                    }

                    if (booking.CustomFields != null && booking.CustomFields.Count > 0)
                    {
                        for (int cField = 0; cField <= booking.CustomFields.Count - 1; cField++)
                        {
                            if (booking.CustomFields[cField].Label == "Camper stored with MBTP? (if yes, enter ID number)")
                            {
                                booking.StoredMBTP = booking.CustomFields[cField].Value;
                                if (booking.Equipment != null && booking.Equipment.Count > 0)
                                {
                                    if (booking.Equipment[0].equipment_make is not null) { booking.EquipmentMake = booking.Equipment[0].equipment_make; }
                                    if (booking.Equipment[0].equipment_model is not null) { booking.EquipmentModel = booking.Equipment[0].equipment_model; }
                                    if (booking.Equipment[0].equipment_length is not null) { booking.EquipmentLength = booking.Equipment[0].equipment_length; }
                                }
                            }
                            else if (booking.CustomFields[cField].Label == "Camper being delivered by outside company? (if yes, enter company name)")
                            {
                                booking.StoredOutside = booking.CustomFields[cField].Value;
                            }
                            else if (booking.CustomFields[cField].Label == "Wristbands")
                            {
                                int wristbands;
                                if (int.TryParse(booking.CustomFields[cField].Value, out wristbands))
                                {
                                    booking.Wristbands = wristbands;
                                }
                                else
                                {
                                    booking.Wristbands = 0;
                                 }
                            }
                        }
                    }
                    if (booking.BookingAdults + booking.BookingChildren + booking.BookingInfants != 0)
                    {
                        bookings.Add(booking);
                    }
                    else
                    {
                        Console.WriteLine("Booking ID " + booking.BookingID + " not added");
                    }
                }
            }
            return bookings;
        }
    }
}