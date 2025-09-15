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
    public class BookingAPI
    {
        private readonly string apiUrl = "https://api.newbook.cloud/rest/bookings_list";
        private readonly string paymentApiUrl = "https://api.newbook.cloud/rest/payments_list";
        private readonly string subAccountsListApiUrl = "https://api.newbook.cloud/rest/gl_category_list";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly IDatabaseConnectionService _dbConnectionService;
        public BookingAPI(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        // For Bookings Table in DB
        public async Task PopulateBookings(DateTime startDate, DateTime endDate)
        {
            Console.WriteLine("Run method started.");

            var bookings = await FetchAllBookingsAsync(startDate, endDate);

            if (bookings.Count > 0)
            {
                using SqlConnection sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                // Insert bookings
                foreach (var booking in bookings)
                {
                    await InsertBookingAsync(booking, sqlConn);
                }

                Console.WriteLine("Total Bookings: " + bookings.Count);
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
                command.Parameters.AddWithValue("@CarLicensePlate", booking.CarLicensePlate ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CarLicensePlateExtra", booking.CarLicensePlateExtra ?? (object)DBNull.Value);
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
            while (dataOffset < dataTotal)
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


                    if (booking.Guests != null && booking.Guests.Count > 0)
                    {
                        var carPlate = booking.Guests.SelectMany(g => g.ContactDetails ?? new List<ContactDetail>()).FirstOrDefault(cd => cd.Type == "car_rego")?.Content;

                        var licenseNotes = booking.Guests.SelectMany(g => g.ContactDetails ?? new List<ContactDetail>()).FirstOrDefault(cd => cd.Type == "car_rego")?.Notes;

                        booking.CarLicensePlate = carPlate;
                        booking.CarLicensePlateExtra = licenseNotes;
                    }


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

        // For CheckedIn table in DB 
        public async Task PopulateCheckIns(DateTime startDate, DateTime endDate)
        {
            Console.WriteLine("Run method started.");

            // Use start of year → endDate to catch all deposits
            var payments = await FetchAllPaymentsAsync(new DateTime(2020, 1, 1), endDate);

            var checkedInList = await FetchAllCheckedInAsync(startDate, endDate, payments);

            if (checkedInList.Count > 0)
            {
                using SqlConnection sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                foreach (var checkedIn in checkedInList)
                {
                    if (checkedIn.BookingStatus != null &&
                        checkedIn.BookingStatus.Equals("arrived", StringComparison.OrdinalIgnoreCase))
                    {
                        await InsertCheckedInAsync(checkedIn, sqlConn);
                    }
                    else
                    {
                        Console.WriteLine($"Skipped Booking {checkedIn.BookingID} with status {checkedIn.BookingStatus}");
                    }
                }
                Console.WriteLine("Total Checked In: " + checkedInList.Count);
            }
            else
            {
                Console.WriteLine("No check-ins to display");
            }
        }



        private async Task InsertCheckedInAsync(Booking checkedIn, SqlConnection sqlConn)
        {
            using (SqlCommand command = new SqlCommand("dbo.UpdateCheckedInTable", sqlConn))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@BookingId", checkedIn.BookingID);
                command.Parameters.AddWithValue("@BookingName", (object?)checkedIn.BookingName ?? DBNull.Value);
                command.Parameters.AddWithValue("@SiteName", checkedIn.SiteName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@BookingStatus", checkedIn.BookingStatus ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CalculatedStayCost", checkedIn.CalculatedStayCost);
                command.Parameters.AddWithValue("@DepositsHeld", checkedIn.DepositsHeld ?? 0);
                command.Parameters.AddWithValue("@AccountBalance", checkedIn.AccountBalance == null ? (object)DBNull.Value : checkedIn.AccountBalance);
                command.Parameters.AddWithValue("@BookingArrival", checkedIn.BookingArrival ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@BookingDeparture", checkedIn.BookingDeparture ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CarLicensePlate", checkedIn.CarLicensePlate ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CarLicensePlateExtra", checkedIn.CarLicensePlateExtra ?? (object)DBNull.Value);

                command.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;

                await command.ExecuteNonQueryAsync();
            }
        }

       public async Task<List<Booking>> FetchAllCheckedInAsync(DateTime startDate, DateTime endDate, List<Payment> payments)
    {
        var periodFrom = startDate.ToString("yyyy-MM-dd HH:mm:ss");
        var periodTo = endDate.ToString("yyyy-MM-dd HH:mm:ss");
        var dataOffset = 0;
        var dataCount = 100;
        var dataTotal = 100000;
        var checkedInList = new List<Booking>();

        var depositsByBooking = payments
        .Where(p => p.AccountFor == "bookings"
                && p.AccountForId.HasValue
                && p.VoidedWhen == null)
        .GroupBy(p => p.AccountForId.Value)
        .ToDictionary(
            g => g.Key, // BookingID
            g => g.Sum(p =>
            {
                // Prefer detailed charges/credits if available
                var charges = p.Charges?.Sum(c => c.Amount) ?? 0m;
                var credits = p.Credits?.Sum(c => c.Amount) ?? 0m;

                if (charges != 0m || credits != 0m)
                    return charges - credits;

                // Otherwise, just use top-level payment amount
                return p.Amount ?? 0m;
            })
        );


        while (dataOffset < dataTotal)
        {
            var requestBody = new
            {
                region = region,
                api_key = apiKey,
                period_from = periodFrom,
                period_to = periodTo,
                list_type = "arrived",
                data_offset = dataOffset,
                data_count = dataCount,
                client_account_booking_details = true,
                client_account_booking_breakdown = true,
            };

            int loopCount = 0;
            HttpResponseMessage response = new HttpResponseMessage();

            while (loopCount < 5)
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
                var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                Console.WriteLine("Sending HTTP POST request for data offset " + dataOffset + "...");

                response = await httpClient.PostAsync(apiUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                    loopCount++;
                    if (loopCount == 5)
                    {
                        return checkedInList;
                    }
                }
                else
                {
                    break;
                }
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            JObject jsonObject = JObject.Parse(jsonResponse);

            foreach (var jsonToken in jsonObject.Children<JProperty>())
            {
                if (jsonToken.Name == "data_total")
                    dataTotal = (int)jsonToken.Value;
                else if (jsonToken.Name == "data_count")
                    dataOffset += (int)jsonToken.Value;
            }

            var result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

            if (result is null || result.success != "true")
            {
                Console.WriteLine("API response indicates failure.");
                return new List<Booking>();
            }

            foreach (var item in result.data)
            {
                var checkedIn = new Booking
                {
                    BookingID = item.booking_id,
                    Firstname = item.firstname,
                    Lastname = item.lastname,
                    SiteName = item.site_name,
                    BookingArrival = item.booking_arrival,
                    BookingDeparture = item.booking_departure,
                    BookingStatus = item.booking_status,
                    BookingTotal = (decimal)item.booking_total,
                    AccountBalance = (decimal)item.account_balance,
                    InventoryItems = JsonConvert.DeserializeObject<List<InventoryItem>>(item.inventory_items?.ToString() ?? "[]"),
                    TariffsQuoted = JsonConvert.DeserializeObject<List<TariffQuoted>>(item.tariffs_quoted?.ToString() ?? "[]"),
                    Guests = JsonConvert.DeserializeObject<List<Guests>>(item.guests?.ToString() ?? "[]"),
                };
                
                if (depositsByBooking.TryGetValue(checkedIn.BookingID, out var totalDeposits))
                    {
                        checkedIn.DepositsHeld = totalDeposits;
                    }
                    else
                    {
                        checkedIn.DepositsHeld = 0m;
                    }


                checkedIn.BookingName = !string.IsNullOrWhiteSpace(checkedIn.Firstname) || !string.IsNullOrWhiteSpace(checkedIn.Lastname)
                    ? $"{checkedIn.Firstname} {checkedIn.Lastname}".Trim()
                    : (checkedIn.Guests?.FirstOrDefault() is Guests g
                        ? $"{g.Firstname} {g.Lastname}".Trim()
                        : null);

                decimal baseStayCost = checkedIn.TariffsQuoted?.Sum(t => t.CalculatedAmount) ?? 0;
                decimal taxTotal = baseStayCost * 0.12m;
                decimal lockFee = checkedIn.InventoryItems?.Where(i => i.Description?.Contains("Site Selection", StringComparison.OrdinalIgnoreCase) == true).Sum(i => i.Amount) ?? 0;

                checkedIn.CalculatedStayCost = baseStayCost + taxTotal + lockFee;

                // License Plate info
                if (checkedIn.Guests != null && checkedIn.Guests.Count > 0)
                {
                    var carPlate = checkedIn.Guests.SelectMany(g => g.ContactDetails ?? new List<ContactDetail>())
                                                .FirstOrDefault(cd => cd.Type == "car_rego")?.Content;

                    var licenseNotes = checkedIn.Guests.SelectMany(g => g.ContactDetails ?? new List<ContactDetail>())
                                                    .FirstOrDefault(cd => cd.Type == "car_rego")?.Notes;

                    checkedIn.CarLicensePlate = carPlate;
                    checkedIn.CarLicensePlateExtra = licenseNotes;
                }

                checkedInList.Add(checkedIn);
            }
        }
        return checkedInList;
    }


        public async Task<List<Payment>> FetchAllPaymentsAsync(DateTime startDate, DateTime endDate)
        {
            var payments = new List<Payment>();
            var periodFrom = startDate.ToString("yyyy-MM-dd HH:mm:ss");
            var periodTo = endDate.ToString("yyyy-MM-dd HH:mm:ss");

            var requestBody = new
            {

                region = region,
                api_key = apiKey,
                period_from = periodFrom,
                period_to = periodTo
            };

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(paymentApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Payments API failed: {response.StatusCode}");
                return payments;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

            foreach (var item in result.data)
            {
                /*
                Console.WriteLine("-------------------------------------------------------------------");
                Console.WriteLine("Id JSON: " + item.id?.ToString());
                Console.WriteLine("AccountId JSON: " + item.account_id?.ToString());
                Console.WriteLine("AccountFor JSON: " + item.account_for?.ToString());
                Console.WriteLine("AccountForID JSON: " + item.account_for_id?.ToString());
                Console.WriteLine("AccountForName JSON: " + item.account_for_name?.ToString());
                Console.WriteLine("Description JSON: " + item.description?.ToString());
                Console.WriteLine("Amount JSON: " + item.amount?.ToString());
                Console.WriteLine("-------------------------------------------------------------------");           
                */
                if (item.account_for_id == "365440" || item.account_for_id == 365440)
                {
                    Console.WriteLine("Full Payment JSON: " + item.ToString());
                }


                var paymentsList = new Payment
                {
                    Id = int.TryParse((string?)item.id, out var idVal) ? idVal : (int?)null,
                    AccountId = item.account_id,
                    AccountFor = item.account_for,
                    AccountForId = int.TryParse((string?)item.account_for_id, out var accIdVal) ? accIdVal : (int?)null,
                    AccountForName = item.account_for_name,
                    GlCategoryId = int.TryParse((string?)item.gl_category_id, out var glId) ? glId : (int?)null,
                    GlCategoryName = item.gl_category_name,
                    Description = item.description,
                    Amount = item.amount,
                    AppliedItems = JsonConvert.DeserializeObject<List<AppliedItems>>(item.applied_items?.ToString() ?? "[]"),
                    Charges = JsonConvert.DeserializeObject<List<Charges>>(item.charges?.ToString() ?? "[]"),
                };


                var categories = await FetchGlCategoriesAsync();

                if (paymentsList.GlCategoryId != null && categories.TryGetValue(paymentsList.GlCategoryId.Value, out string name))
                {
                    paymentsList.GlCategoryName = name;
                }


                if (paymentsList.AppliedItems != null)
                {
                    foreach (var applied in paymentsList.AppliedItems)
                    {
                        if (applied.Charges != null)
                        {
                            foreach (var charge in applied.Charges)
                            {
                                Console.WriteLine($"Charge: {charge.Id}, Period: {charge.PeriodFrom} - {charge.PeriodTo}");
                            }
                        }

                        if (applied.Credits != null)
                        {
                            foreach (var credit in applied.Credits)
                            {
                                Console.WriteLine($"Description: {credit.Description}, Amount: {credit.Amount}");
                            }
                        }
                    }
                }
                
                Console.WriteLine($"Name={paymentsList.AccountForName}, " +
                $"Payment BookingId={paymentsList.AccountForId}, " +
                  $"Desc={paymentsList.Description}, " +
                  $"Amount={paymentsList.Amount}, " +
                  $"GL={paymentsList.GlCategoryId}");


                //Console.WriteLine($"AccountForId (typed): {paymentsList.AccountForId}");


                payments.Add(paymentsList);
            }

            return payments;
        }
        
        public async Task<Dictionary<int, string>> FetchGlCategoriesAsync()
        {
            var requestBody = new
            {
                region = region,
                api_key = apiKey
            };

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
            httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(subAccountsListApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"GL Categories API failed: {response.StatusCode}");
                return new Dictionary<int, string>();
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

            var categories = new Dictionary<int, string>();
            foreach (var item in result.data)
            {
                if (int.TryParse((string?)item.gl_category_id, out int id))
                {
                    categories[id] = (string)item.gl_category_name;
                }
            }

            return categories;
        }


    }
}

// everything first goes to deposits
// if payment made today == arival date: deposits -> income
