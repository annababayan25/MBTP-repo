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
    public class CheckedInApi : NewbookBaseApi
    {
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly IDatabaseConnectionService _dbConnectionService;

        public CheckedInApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }
        
        // For CheckedIn table in DB 
        public async Task PopulateCheckIns(DateTime startDate, DateTime endDate)
        {
            var body = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                data_offset = 0,
                data_count = 100,
                list_type = "arrived",
                client_account_booking_details = "true",
                client_account_item_breakdown = "true",
                account_breakdown = "true"

            };


            var json = await PostAsync("bookings_list", body);

            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            var checkedInList = new List<CheckedIn>();

            foreach (var item in result.data)
            {
                var checkedIn = new CheckedIn
                {
                    BookingID = item.booking_id,
                    Firstname = item.firstname,
                    Lastname = item.lastname,
                    Site = item.site_name,
                    CategoryName = item.category_name,
                    BookingArrival = item.booking_arrival,
                    BookingDeparture = item.booking_departure,
                    BookingStatus = item.booking_status,
                    BookingTotal = (decimal)item.booking_total,
                    AccountBalance = (decimal)item.account_balance,
                    DepositsHeld = 0,
                    BookingCheckedIn = item.booking_checkedin,
                    InventoryItems = JsonConvert.DeserializeObject<List<InventoryItem>>(item.inventory_items?.ToString() ?? "[]"),
                    TariffsQuoted = JsonConvert.DeserializeObject<List<TariffQuoted>>(item.tariffs_quoted?.ToString() ?? "[]"),
                    Guests = JsonConvert.DeserializeObject<List<Guests>>(item.guests?.ToString() ?? "[]"),
                    Charges = JsonConvert.DeserializeObject<List<Charges>>(item.charges?.ToString() ?? "[]"),
                    Credits = JsonConvert.DeserializeObject<List<Credit>>(item.credits?.ToString() ?? "[]"),
                    Payments = JsonConvert.DeserializeObject<List<Payment>>(item.payments?.ToString() ?? "[]"),
                    Refunds = JsonConvert.DeserializeObject<List<Refund>>(item.refunds?.ToString() ?? "[]"),
                    Taxes = JsonConvert.DeserializeObject<List<Tax>>(item.taxes?.ToString() ?? "[]")
                };

                // BookingName
                checkedIn.BookingName = !string.IsNullOrWhiteSpace(checkedIn.Firstname) || !string.IsNullOrWhiteSpace(checkedIn.Lastname)
                ? $"{checkedIn.Firstname} {checkedIn.Lastname}".Trim()
                : (checkedIn.Guests?.FirstOrDefault() is Guests g
                    ? $"{g.Firstname} {g.Lastname}".Trim()
                    : null);

                // CalculatedStayCost logic 

                decimal cleaningFee = checkedIn.Charges?
                    .Where(c => c.Description?.IndexOf("cleaning fee", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Sum(c => c.Amount ?? 0) ?? 0;

                // cancellation fee column - included in CSC and DH
                decimal cancellationFee = checkedIn.InventoryItems?
                .Where(c => (c.Description?.Contains("cancellation", StringComparison.OrdinalIgnoreCase) ?? false))
                .Sum(c => decimal.TryParse(c.Amount, out var amt) ? amt : 0) ?? 0;

                checkedIn.CancellationFee = cancellationFee;

                // lock fee column
                var lockFeeChargeIds = checkedIn.Charges?
                    .Where(c => !string.IsNullOrEmpty(c.Description) &&
                        (c.Description.Contains("lock fee", StringComparison.OrdinalIgnoreCase) ||
                        c.Description.Contains("site selection", StringComparison.OrdinalIgnoreCase)))
                    .Select(c => c.Id)
                    .ToHashSet() ?? new HashSet<string>();

                decimal lockFeePaid = checkedIn.Payments?
                    .SelectMany(p => p.PaymentCharges ?? new List<PaymentChargeLink>())
                    .Where(pc => lockFeeChargeIds.Contains(pc.ChargeId.ToString()))
                    .Sum(pc => pc.ReconciledAmount) ?? 0;

                if (lockFeePaid == 0)
                {
                    lockFeePaid = checkedIn.Charges?
                        .Where(c => lockFeeChargeIds.Contains(c.Id))
                        .Sum(c => c.Amount ?? 0) ?? 0;
                }
                checkedIn.LockFee = lockFeePaid;

                // Online booking fee column
                decimal onlineBookingFee = checkedIn.Charges?
                .Where(c => c.Description?.Contains("online booking fee", StringComparison.OrdinalIgnoreCase) == true)
                .Sum(c => c.Amount ?? 0) ?? 0;
                checkedIn.OnlineBookingFee = onlineBookingFee;

                decimal totalRefundAmount = checkedIn.Refunds?.Sum(r => r.Amount ?? 0) ?? 0;
                checkedIn.RefundedAmount = totalRefundAmount;

                decimal mergedDeposits = checkedIn.Payments?
                .Where(p =>
                    !string.IsNullOrEmpty(p.Description) &&
                    (
                        p.Description.Contains("accommodation", StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains("extra vehicle", StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains("storage", StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains("balance transfer", StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains("mobile home", StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains("lease", StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains("booking #", StringComparison.OrdinalIgnoreCase) ||
                        (p.Description.Contains("deposit", StringComparison.OrdinalIgnoreCase) &&
                        !p.Description.Contains("security deposit", StringComparison.OrdinalIgnoreCase))
                    )
                    && (checkedIn.BookingCheckedIn.HasValue
                    ? p.GeneratedWhen < checkedIn.BookingCheckedIn.Value : true))
                    .Sum(p => p.Amount ?? 0) ?? 0;

                // security deposit column
                decimal totalPaymentsSecDep = checkedIn.Payments?
                .Where(p => !string.IsNullOrEmpty(p.Description) &&
                        p.Description.Contains("security deposit", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount ?? 0) ?? 0;

                decimal totalChargesSecDep = checkedIn.Charges?
                .Where(p => !string.IsNullOrEmpty(p.Description) &&
                        p.Description.Contains("security deposit", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount ?? 0) ?? 0;

                checkedIn.SecurityDeposits = totalChargesSecDep + totalPaymentsSecDep;

                var afterCheckInPayments = checkedIn.Payments?
                .Where(p => p.GeneratedWhen > checkedIn.BookingCheckedIn.Value)
                .ToList();

                checkedIn.PaymentsAfterCheckIn = afterCheckInPayments?.Sum(p => p.Amount ?? 0) ?? 0;
                checkedIn.PaymentsAfterCheckInDesc = afterCheckInPayments != null
                ? string.Join("; ", afterCheckInPayments.Select(p => $"{p.Amount:C} ({p.Description})"))
                : null;

                // Calculate the Deposits Held
                checkedIn.DepositsHeld = mergedDeposits + cancellationFee - lockFeePaid - onlineBookingFee - totalRefundAmount;

                // Calculate the Stay Cost 
                decimal baseStayCost = checkedIn.TariffsQuoted?.Sum(t => t.CalculatedAmount) ?? 0;
                decimal discounts = checkedIn.Credits?.Where(c => c.VoidedWhen == null).Sum(c => c.Amount ?? 0) ?? 0;

                if (discounts == 0)
                {
                    decimal taxTotal = checkedIn.TariffsQuoted?.Sum(t => t.Taxes?.Sum(tx => tx.TaxAmount ?? 0) ?? 0) ?? 0;
                    checkedIn.CalculatedStayCost = baseStayCost + taxTotal + cleaningFee;
                }
                else
                {
                    decimal baseTotalAfterDiscount = baseStayCost - discounts;
                    decimal baseTaxRate = checkedIn.TariffsQuoted?.Sum(t => t.Taxes?.Sum(tx => tx.TaxAmount ?? 0) ?? 0) ?? 0;
                    decimal taxRateAfterCredit = checkedIn.Credits?.Where(c => c.VoidedWhen == null).Sum(t => t.Taxes?.Sum(tx => tx.TaxAmount ?? 0) ?? 0) ?? 0;
                    decimal effectiveTaxRate = baseTaxRate - taxRateAfterCredit;
                    checkedIn.CalculatedStayCost = baseTotalAfterDiscount + effectiveTaxRate + cleaningFee;
                }


                // Handle inventory items not present in charges or tariffs
                if (checkedIn.InventoryItems != null && checkedIn.InventoryItems.Count > 0)
                {
                    var chargeInventoryIds = checkedIn.Charges?
                        .Where(c => !string.IsNullOrEmpty(c.InventoryItemId))
                        .Select(c => c.InventoryItemId)
                        .ToHashSet() ?? new HashSet<string>();

                    var tariffIds = checkedIn.TariffsQuoted?
                        .Where(t => t != null && t.Id != null)
                        .Select(t => t.Id.ToString())
                        .ToHashSet() ?? new HashSet<string>();

                    var extraInventoryItems = checkedIn.InventoryItems
                        .Where(inv =>
                            !chargeInventoryIds.Contains(inv.InventoryItemId) &&
                            !tariffIds.Contains(inv.InventoryItemId))
                            .ToList();

                    if (extraInventoryItems.Count > 0)
                    {
                        decimal extraAmount = extraInventoryItems
                        .Sum(inv => decimal.TryParse(inv.Amount, out var amt) ? amt : 0);

                        checkedIn.CalculatedStayCost = (checkedIn.CalculatedStayCost ?? 0) + extraAmount;
                    }
                }

                if ((checkedIn.CalculatedStayCost ?? 0) == 0 && (checkedIn.DepositsHeld ?? 0) > 0)
                {
                    checkedIn.CalculatedStayCost = checkedIn.DepositsHeld;
                }

                // License Plate info
                if (checkedIn.Guests != null && checkedIn.Guests.Count > 0)
                {
                    var carPlate = checkedIn.Guests.SelectMany(g => g.ContactDetails ?? new List<ContactDetail>()).FirstOrDefault(cd => cd.Type == "car_rego")?.Content;

                    var licenseNotes = checkedIn.Guests.SelectMany(g => g.ContactDetails ?? new List<ContactDetail>()).FirstOrDefault(cd => cd.Type == "car_rego")?.Notes;

                    checkedIn.CarLicensePlate = carPlate;
                    checkedIn.CarLicensePlateExtra = licenseNotes;
                }


                // debug block to see what payments were made after check in
                /*
                if (checkedIn.BookingID == // add booking id here)
                {
                if (checkedIn.BookingCheckedIn.HasValue && checkedIn.Payments != null)
                    {
                        var aftercheckin = checkedIn.Payments
                            .Where(p => p.GeneratedWhen > checkedIn.BookingCheckedIn.Value)
                            .ToList();

                        if (aftercheckin.Any())
                        {
                            Console.WriteLine($"Booking {checkedIn.BookingID} - Payments generated AFTER check-in:");
                            foreach (var p in aftercheckin)
                            {
                                Console.WriteLine($"  PaymentId: {p.Id} | Desc: {p.Description} | Amount: {p.Amount} | GeneratedWhen: {p.GeneratedWhen}");
                            }
                        }
                    }
                }*/

                // debug to see the full json for a booking
                if (checkedIn.BookingID == 374903)
                {
                    string filePath = "output.txt";
                    string contentFile = item.ToString();
                    File.WriteAllText(filePath, contentFile + Environment.NewLine);
                }

                checkedInList.Add(checkedIn);
            }

            using var sqlConn = _dbConnectionService.CreateConnection();
            await sqlConn.OpenAsync();

            foreach (var checkedIn in checkedInList)
            {
                using (SqlCommand command = new SqlCommand("dbo.UpdateCheckedInTable", sqlConn))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BookingId", checkedIn.BookingID);
                    command.Parameters.AddWithValue("@BookingName", (object?)checkedIn.BookingName ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Site", $"{checkedIn.CategoryName} {checkedIn.Site}" ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@BookingStatus", checkedIn.BookingStatus ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CalculatedStayCost", checkedIn.CalculatedStayCost);
                    command.Parameters.AddWithValue("@DepositsHeld", checkedIn.DepositsHeld);
                    command.Parameters.AddWithValue("@LockFee", checkedIn.LockFee);
                    command.Parameters.AddWithValue("@SecurityDeposits", checkedIn.SecurityDeposits);
                    command.Parameters.AddWithValue("@OnlineBookingFee", checkedIn.OnlineBookingFee);
                    command.Parameters.AddWithValue("@PaymentsAfterCheckIn", checkedIn.PaymentsAfterCheckInDesc);
                    command.Parameters.AddWithValue("@Refunds", checkedIn.RefundedAmount);
                    command.Parameters.AddWithValue("@CancellationFee", checkedIn.CancellationFee);
                    command.Parameters.AddWithValue("@AccountBalance", checkedIn.AccountBalance == null ? (object)DBNull.Value : checkedIn.AccountBalance);
                    command.Parameters.AddWithValue("@BookingArrival", checkedIn.BookingArrival ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@BookingCheckedIn", (object?)checkedIn.BookingCheckedIn ?? DBNull.Value);
                    command.Parameters.AddWithValue("@BookingDeparture", checkedIn.BookingDeparture ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CarLicensePlate", checkedIn.CarLicensePlate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CarLicensePlateExtra", checkedIn.CarLicensePlateExtra ?? (object)DBNull.Value);
                    command.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }

}