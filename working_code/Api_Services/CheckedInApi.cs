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
        private readonly IDatabaseConnectionService _dbConnectionService;
        

        public CheckedInApi(HttpClient client, IDatabaseConnectionService dbConnectionService, CheckedInListRepo checkedInListRepo) : base(client)
        {
            _dbConnectionService = dbConnectionService;
            
        }

        // For CheckedIn table in DB 
        public async Task<List<CheckedIn>> PopulateCheckIns(DateTime startDate, DateTime endDate)
        {
            var dataOffset = 0;
            var dataCount = 100;
            var dataTotal = 100000;
            var checkedInList = new List<CheckedIn>();

            while (dataOffset < dataTotal)
            {
                var body = new
                {
                    region = region,
                    api_key = apiKey,
                    period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    list_type = "arrived",
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
                    var checkedIn = new CheckedIn
                    {
                        BookingId = item.booking_id,
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
                        PaymentsAfterCheckIn = 0.0m,
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

                    // cleaning fee 
                    decimal cleaningFee = checkedIn.Charges?
                        .Where(c => c.Description?.IndexOf("cleaning fee", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Sum(c => c.Amount ?? 0) ?? 0;

                    // cancellation fee column - included in CSC and DH
                    decimal cancellationFee = checkedIn.InventoryItems?
                    .Where(c => c.Description != null && (c.Description?.Contains("cancellation", StringComparison.OrdinalIgnoreCase) ?? false))
                    .Sum(c => decimal.TryParse(c.Amount, out var amt) ? amt : 0) ?? 0;

                    checkedIn.CancellationFee = cancellationFee;

                    // lock fee column
                    var lockFeeChargeIds = checkedIn.Charges?
                        .Where(c => !string.IsNullOrEmpty(c.Description) &&
                            (c.Description.Contains("lock fee", StringComparison.OrdinalIgnoreCase) ||
                            c.Description.Contains("site selection", StringComparison.OrdinalIgnoreCase))
                            && c.VoidedWhen == null)
                        .Select(c => c.Id)
                        .ToHashSet();

                    decimal lockFeePaid = checkedIn.Payments?
                        .SelectMany(p => p.PaymentCharges ?? new List<PaymentChargeLink>())
                        .Where(pc => lockFeeChargeIds.Contains(pc.ChargeId))
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
                    .Where(c => c.Description?.Contains("online booking fee", StringComparison.OrdinalIgnoreCase) == true &&
                    (c.VoidedWhen == null))
                    .Sum(c => c.Amount ?? 0) ?? 0;
                    checkedIn.OnlineBookingFee = onlineBookingFee;


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
                            p.Description.Contains("golf", StringComparison.OrdinalIgnoreCase) ||
                            p.Description.Contains("deposit", StringComparison.OrdinalIgnoreCase) ||
                            !p.Description.Contains("security", StringComparison.OrdinalIgnoreCase)
                        ) &&
                        (p.Deposit == "1") &&
                        (checkedIn.BookingCheckedIn.HasValue
                            ? p.GeneratedWhen < checkedIn.BookingCheckedIn.Value
                            : true))
                    .Sum(p => p.Amount ?? 0) ?? 0;

                    // security deposit column
                    decimal totalPaymentsSecDep = checkedIn.Payments?
                    .Where(c =>
                        !string.IsNullOrEmpty(c.Description) &&
                        c.Description.Contains("security deposit", StringComparison.OrdinalIgnoreCase) &&

                        (
                            c.VoidedWhen == null ||
                            (checkedIn.BookingCheckedIn.HasValue && c.VoidedWhen >= checkedIn.BookingCheckedIn.Value)
                        ) &&

                        c.GeneratedWhen.HasValue &&
                        checkedIn.BookingCheckedIn.HasValue &&
                        c.GeneratedWhen.Value.Date == checkedIn.BookingCheckedIn.Value.Date
                    )
                    .Sum(c => c.Amount ?? 0) ?? 0;


                    decimal totalChargesSecDep = checkedIn.Charges?
                    .Where(c =>
                        !string.IsNullOrEmpty(c.Description) &&
                        c.Description.Contains("security deposit", StringComparison.OrdinalIgnoreCase) &&

                        (
                            c.VoidedWhen == null ||
                            (checkedIn.BookingCheckedIn.HasValue && c.VoidedWhen >= checkedIn.BookingCheckedIn.Value)
                        ) &&

                        c.GeneratedWhen.HasValue &&
                        checkedIn.BookingCheckedIn.HasValue &&
                        c.GeneratedWhen.Value.Date == checkedIn.BookingCheckedIn.Value.Date
                    )
                    .Sum(c => c.Amount ?? 0) ?? 0;



                    checkedIn.SecurityDeposits = totalChargesSecDep + totalPaymentsSecDep;


                    var afterCheckInPayments = checkedIn.Payments?
                    .Where(p => p.GeneratedWhen.HasValue &&
                                checkedIn.BookingCheckedIn.HasValue &&
                                p.GeneratedWhen.Value.Date == checkedIn.BookingCheckedIn.Value.Date &&
                                p.GeneratedWhen.Value >= checkedIn.BookingCheckedIn.Value)
                    .ToList();

                checkedIn.PaymentsAfterCheckIn = afterCheckInPayments?.Sum(p => p.Amount ?? 0) ?? 0;
                checkedIn.PaymentsAfterCheckInDesc = afterCheckInPayments != null && afterCheckInPayments.Any()
                    ? string.Join("; ", afterCheckInPayments.Select(p => $"({p.Description})"))
                    : null;

                    decimal totalRefundAmount = 0m;

                    if (checkedIn.Refunds != null && checkedIn.BookingCheckedIn.HasValue)
                    {
                        totalRefundAmount = checkedIn.Refunds
                            .Where(r => r.GeneratedWhen.HasValue &&
                                        r.GeneratedWhen.Value < checkedIn.BookingCheckedIn.Value)
                            .Sum(r => r.Amount ?? 0m);
                    }
                    checkedIn.RefundedAmount = totalRefundAmount;

                    // Calculate the Deposits Held
                    checkedIn.DepositsHeld = mergedDeposits + cancellationFee - lockFeePaid - onlineBookingFee - totalRefundAmount;

                    decimal clientDebit = 0m;
                    if (checkedIn.Refunds != null && checkedIn.BookingCheckedIn.HasValue)
                    {
                        clientDebit = checkedIn.Refunds
                            .Where(r => r.GeneratedWhen.HasValue &&
                                        r.GeneratedWhen.Value.Date == checkedIn.BookingCheckedIn.Value.Date)
                            .Sum(r => r.Amount ?? 0m);

                        checkedIn.DepositsHeld = checkedIn.DepositsHeld - clientDebit;
                    } 

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

                    // Handle inventory items and avoid double counting
                    if (checkedIn.InventoryItems != null && checkedIn.InventoryItems.Count > 0)
                    {
                        // get charges Inventory Ids if exists
                        var chargeInventoryIds = checkedIn.Charges?
                            .Where(c => c.InventoryItemId != null)
                            .Select(c => c.InventoryItemId)
                            .ToHashSet();

                        // get all inventory items except the following descriptions
                        var inventoryItems = checkedIn.InventoryItems
                        .Where(inv => (
                                inv.Description.Contains("Concrete Pad", StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                        // Get tax amounts for inventory items from  Charges if exist  bc inventory doesnt have a tax field
                        var inventoryTaxInChargesTotal = checkedIn.Charges
                            .Where(inv => chargeInventoryIds.Contains(inv.InventoryItemId))
                            .SelectMany(charge => charge.Taxes).Sum(tax => tax.TaxAmount);

                        // Add linked inventory item amounts to stay cost
                        if (inventoryItems.Count > 0)
                        {
                            decimal linkedAmount = inventoryItems.Sum(inv => decimal.TryParse(inv.Amount, out var amt) ? amt : 0);
                            checkedIn.CalculatedStayCost = (checkedIn.CalculatedStayCost ?? 0) + linkedAmount + inventoryTaxInChargesTotal;
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

                        }*/

                    // debug to see the full json for a booking

                    if (checkedIn.BookingId == 372922)
                    {
                        string filePath = "checkedinOut.txt";
                        string contentFile = item.ToString();
                        File.WriteAllText(filePath, contentFile + Environment.NewLine);
                    }


                    // Only proceed if refund should apply the same day as check-in

                    // Only run if  refunds (if any) were created the same day as check-in
                    bool sameDayRefunds = checkedIn.Refunds == null ||
                        checkedIn.Refunds.All(r =>
                            r.GeneratedWhen.HasValue &&
                            r.GeneratedWhen.Value.Date == checkedIn.BookingCheckedIn.Value.Date && r.GeneratedWhen.Value >= checkedIn.BookingCheckedIn.Value);

                    if (sameDayRefunds && checkedIn.DepositsHeld < 0)
                    {
                        checkedIn.RefundedAmount = (checkedIn.RefundedAmount ?? 0) + checkedIn.DepositsHeld;
                        checkedIn.Extras = checkedIn.DepositsHeld.ToString();
                        checkedIn.DepositsHeld = 0.0m;

                        if (!string.IsNullOrEmpty(checkedIn.Extras) && (checkedIn.Extras.Contains("-43") || checkedIn.Extras.Contains("-40") || checkedIn.Extras.Contains("-46") || checkedIn.Extras.Contains("-3")))
                        {
                            checkedIn.Extras = $"{checkedIn.Extras} (Possible balance transfer?)";
                        }
                    }
                    checkedInList.Add(checkedIn);
                    //var jsonOutput = JsonConvert.SerializeObject(checkedInList, Formatting.Indented);
                    //File.AppendAllText("checkedInList.json", jsonOutput);
                    
                }
            }
            return checkedInList;
        }
        
    }
}
