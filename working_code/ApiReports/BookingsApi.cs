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
    public class BookingApi : NewbookBaseApi
    {
        private readonly IDatabaseConnectionService _dbConnectionService;
        public BookingApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task PopulateBookings(DateTime startDate, DateTime endDate)
        {
            var dataOffset = 0;
            var dataCount = 500;
            var dataTotal = 100000;
            var bookings = new List<Booking>();

            while (dataOffset < dataTotal)
            {
                var body = new
                {
                    region = region,
                    api_key = apiKey,
                    period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    list_type = "all",
                    data_offset = dataOffset,
                    data_count = dataCount,
                    client_account_booking_details = "true",
                    client_account_item_breakdown = "true",
                    account_breakdown = "true"
                };

                var json = await PostAsync("bookings_list", body);
                var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());

                Console.WriteLine($"Sending request at offset {dataOffset} of {dataTotal} (batch size {dataCount})");

                if (result == null || result.success != "true") break;
                dataTotal = result.data_total;
                dataOffset += dataCount;


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
                        Equipment = JsonConvert.DeserializeObject<List<EquipmentFields>>(item.equipment.ToString()), // Deserialize the equipment fields list
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

                    if (booking.BookingID == 366736)
                    {
                        string filePath = "booking.txt";
                        string contentFile = item.ToString();
                        File.WriteAllText(filePath, contentFile + Environment.NewLine);
                    }

                }

                using var sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                foreach (var booking in bookings)
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
                bookings.Clear();
            }
           Console.WriteLine("Run method finished.");
        }
    }
}