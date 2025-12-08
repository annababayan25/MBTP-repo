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

        public async Task<List<Booking>> PopulateBookings(DateTime startDate, DateTime endDate)
        {
            var dataOffset = 0;
            var dataCount = 500;
            var dataTotal = 100000;
            var bookings = new List<Booking>();

            while (dataOffset < dataTotal)
            {
                var requestBody = new
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

                var json = await PostAsync("bookings_list", requestBody);
                var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());

                Console.WriteLine($"Sending request at offset {dataOffset} of {dataTotal} (batch size {dataCount})");

                if (result == null || result.success != "true") break;
                dataTotal = result.data_total;
                dataOffset += dataCount;


                foreach (var item in result.data)
                {
                    var booking = new Booking
                    {
                        BookingId = item.booking_id,
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
                        LockFee = 0.0m,
                        Guests = JsonConvert.DeserializeObject<List<Guests>>(item.guests.ToString()), // Deserialize the guests list
                        CustomFields = JsonConvert.DeserializeObject<List<CustomFields>>(item.custom_fields.ToString()), // Deserialize the custom fields list
                        Equipment = JsonConvert.DeserializeObject<List<EquipmentFields>>(item.equipment.ToString()), // Deserialize the equipment fields list
                        Charges = JsonConvert.DeserializeObject<List<Charges>>(item.charges.ToString()),
                        Payments = JsonConvert.DeserializeObject<List<Payment>>(item.payments.ToString()),
                        InventoryItems = JsonConvert.DeserializeObject<List<InventoryItem>>(item.inventory_items.ToString())

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
                    // lock fee column
                    // 1. Find charges that match
                    var lockFeeChargeIds = booking.Charges?
                        .Where(c => c.Description != null &&
                                    (c.Description.Contains("lock fee", StringComparison.OrdinalIgnoreCase) ||
                                    c.Description.Contains("site selection", StringComparison.OrdinalIgnoreCase)) &&
                                    c.VoidedWhen == null)
                        .Select(c => c.Id)
                        .ToHashSet() ?? new HashSet<int?>();

                    decimal lockFeePaid = 0;

                    // 2. Sum payments linked to those charges
                    if (booking.Payments != null)
                    {
                        lockFeePaid = booking.Payments
                            .SelectMany(p => p.PaymentCharges ?? new List<PaymentChargeLink>())
                            .Where(pc => lockFeeChargeIds.Contains(pc.ChargeId))
                            .Sum(pc => pc.ReconciledAmount);
                    }

                    // 3. Fallback: sum charges directly
                    if (lockFeePaid == 0 && booking.Charges != null)
                    {
                        lockFeePaid = booking.Charges
                            .Where(c => lockFeeChargeIds.Contains(c.Id ?? -1))
                            .Sum(c => c.Amount ?? 0);
                    }

                    // 4. FINAL FALLBACK: check inventory items
                    if (lockFeePaid == 0 && booking.InventoryItems != null)
                    {
                        lockFeePaid = booking.InventoryItems
                            .Where(i =>
                                i.Description != null &&
                                (i.Description.Contains("lock fee", StringComparison.OrdinalIgnoreCase) ||
                                i.Description.Contains("site selection", StringComparison.OrdinalIgnoreCase)))
                            .Sum(i => decimal.Parse(i.Amount ?? "0"));
                    }

                    booking.LockFee = lockFeePaid;


                    if (booking.BookingAdults + booking.BookingChildren + booking.BookingInfants != 0)
                    {
                        bookings.Add(booking);
                    }
                    else
                    {
                        Console.WriteLine("Booking ID " + booking.BookingId + " not added");
                    }

                    if(booking.BookingId == 378087)
                    {
                        
                        File.WriteAllText("bookings.json", item.ToString() + Environment.NewLine);
                    }

                }
            }
            
            return bookings;
        }
    }
}