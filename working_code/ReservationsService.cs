using System.Data;
using MBTP.Models;
using Newtonsoft.Json;
using MBTP.Retrieval;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.Json;
using MBTP.Interfaces;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

// ReservationsService is a class dedicated to Reservations Deposits Table (Daily Breakdown R)
namespace MBTP.Services
{
    public class ReservationsService : NewbookBaseApi
    {
        private readonly TransactionFlowApi _transactionFlowApi;
        private readonly DailyReport _dailyReport;
        private readonly IDatabaseConnectionService _dbConnectionService;
        private readonly Dictionary<string, decimal> _bucketTotals = new(); 

        public ReservationsService(IDatabaseConnectionService dbConnectionService, HttpClient client, DailyReport dailyReport, TransactionFlowApi transactionFlowApi)
            : base(client)
        {
            _dbConnectionService = dbConnectionService;
            _transactionFlowApi = transactionFlowApi;
            _dailyReport = dailyReport;
        }

        public async Task<List<Reservations>> ProcessReservationsAsync(DateTime startDate, DateTime endDate)
        {
            // Retrieve the Checked In List DataSet and convert it to a list
            var checkedInList = await GetCheckedInList(startDate, endDate);
            // Call the Transaction Flow Api and retrieve the list
            var transactions = await _transactionFlowApi.PopulateTransactions(startDate, endDate);
            var reservations = new Reservations { TransDate = startDate };
            var reservationsList = new List<Reservations>();

            string[] siteCategories = { "WESC", "Water & Electric Only" };
            string[] rentalCategories = { "Ocean Villa", "Cottage", "Cabin", "Travel Trailer" };
            string[] extrasVehicles = { "Extra Vehicle", "Vehicle", "Vehicles", "Extra Vehicles Fee", "Extra Vehicle Fees", "Extra", "Car" };
            string[] annualLease = { "Annual", "Annual Lease", "ANNUAL LEASE", "A/L" };
            string[] employee = { "Employee" };
            string[] mobileHome = { "Mobile", "Mobile Home", "M/L", "ML" };
            string[] storage = { "Storage" };
            string[] security = { "Security" };
            string[] golfCategories = { "Golf" };

            // --------------- START RESERVATIONS DEPOSITS (DBR) TABLE --------------------
            
            // START: Retrieve Deposits Taken
            // Retrieve all Deposits Taken for the day for SITES and RENTALS from transaction flow
            var depositsTakenList = transactions
            .Where(p =>
                p.Category != null &&
                (
                    siteCategories.Any(c => p.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                    rentalCategories.Any(c => p.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
                )
                &&
                !p.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
                !p.Description.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
                (p.Description.Contains("Deposit", StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains("at Myrtle Beach Travel Park", StringComparison.OrdinalIgnoreCase) ||  
                p.Description.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase)) &&
                p.Deposit != null &&
                p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase) &&
                (p.Description == null || !p.Description.Contains("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase))
            )
            .OrderBy(p => p.AccountForId)
            .ToList();

            // Exclude deposits that have had any refunds for the same PaymentTypeReference + AccountForId on the same day
            var depositsTakenList_NoRefunds = IgnoreEntries("Refunds Raised", depositsTakenList, transactions);
            
            // Only count deposits that were taken before check-in.
            // Filter out "deposits" that were paid after the guest had checked in.
            var confirmedList = depositsTakenList_NoRefunds.Where(t =>
            (t.HasArrived == false) || (t.HasArrived == true && t.TransDate <= t.BookingCheckedIn)).ToList();

            var confirmedSitesList = FilterTransactions(confirmedList, siteCategories).ToList();
            var confirmedRentalsList = FilterTransactions(confirmedList, rentalCategories).ToList();

            
            var RentalDepTaken_Refunds = transactions
            .Where(p =>
                p.PaymentTypeAction == "Refunds" &&
                p.Category != null &&
                rentalCategories.Any(c => 
                    p.Category.Contains(c, StringComparison.OrdinalIgnoreCase)
                ) &&
                p.PaymentMethod == "Authorize.Net" &&
                p.HasArrived == false
            )
            .Sum(p => Math.Abs(p.Amount ?? 0));

            var SiteDepTaken_Refunds = transactions
            .Where(p =>
                p.PaymentTypeAction == "Refunds" &&
                p.Category != null &&
                siteCategories.Any(c => 
                    p.Category.Contains(c, StringComparison.OrdinalIgnoreCase)
                ) &&
                p.PaymentMethod == "Authorize.Net" &&
                p.HasArrived == false
            )
            .Sum(p => Math.Abs(p.Amount ?? 0));
            

            // -------------------------------------------------------------
            // Daily Reservations — Fiscal-Year Allocation Logic (FIXED)
            // -------------------------------------------------------------
            // Goal:
            // Allocate reservation deposits to the correct fiscal year based
            // on *arrival date*, not payment date. The fiscal year (FY)
            // boundary is October 1.
            //
            // Determine the current fiscal year based on startDate:
            // - If startDate is Oct 1 or later, current FY starts this year
            // - Otherwise, current FY started last year

            DateTime currentFyStart;
            if (startDate.Month >= 10)
            {
                currentFyStart = new DateTime(startDate.Year, 10, 1);
            }
            else
            {
                currentFyStart = new DateTime(startDate.Year - 1, 10, 1);
            }

            var fyBuckets = new List<FyBucket>
            {
                new FyBucket  // Next FY (2 years out)
                {
                    FyStart = currentFyStart.AddYears(2),
                    Sites = 0, Rentals = 0, Lock_Fees = 0
                },
                new FyBucket  // Upcoming FY (1 year out)
                {
                    FyStart = currentFyStart.AddYears(1),
                    Sites = 0, Rentals = 0, Lock_Fees = 0
                },
                new FyBucket  // Current FY
                {
                    FyStart = currentFyStart,
                    Sites = 0, Rentals = 0, Lock_Fees = 0
                }
            };

            /// <summary>
            /// Determine which FY bucket an arrival date belongs to.
            /// Buckets are ordered newest to oldest. The first bucket whose
            /// FyStart is <= the arrival date is the correct bucket.
            /// </summary>
            int ResolveBucket(DateTime date)
            {
                for (int i = 0; i < fyBuckets.Count; i++)
                {
                    if (date >= fyBuckets[i].FyStart)
                        return i;
                }

                // If arrival is before all buckets (historical), don't count it
                // or optionally assign to current FY bucket (index 2)
                return -1; // Return -1 to indicate "don't count"
            }

            /// <summary>
            /// Add a transaction deposit to the correct FY bucket based
            /// solely on the arrival date. Negative values are treated as
            /// positive deposits via Abs().
            /// </summary>
            /// <param name="t">Reservation transaction</param>
            /// <param name="isSite">True = Site deposit, False = Rental deposit</param>
            void AssignDeposit(TransactionFlow t, bool isSite)
            {
                if (!t.ArrivalDate.HasValue)
                    return;

                var arr = t.ArrivalDate.Value.Date;
                var amt = Math.Abs(t.Amount ?? 0);

                int idx = ResolveBucket(arr);
                
                // Only assign if valid bucket found
                if (idx < 0)
                    return;

                if (isSite)
                    fyBuckets[idx].Sites += amt;
                else
                    fyBuckets[idx].Rentals += amt;
            }

            // Load deposits into buckets
            foreach (var t in confirmedSitesList)
                AssignDeposit(t, true);

            foreach (var t in confirmedRentalsList)
                AssignDeposit(t, false);
            // Roll-up results:
            //   - Current FY = fyBuckets[2]
            //   - Future FYs = fyBuckets[0] + fyBuckets[1]
            reservations.SiteDepTaken   = fyBuckets[2].Sites - SiteDepTaken_Refunds;
            reservations.SiteDepTakenFuture   = fyBuckets[0].Sites   + fyBuckets[1].Sites;
            reservations.RentalDepTaken = fyBuckets[2].Rentals - RentalDepTaken_Refunds;
            reservations.RentalDepTakenFuture = fyBuckets[0].Rentals + fyBuckets[1].Rentals;
            reservations.GolfDepTaken = CalculateGolfCartDeposits(transactions);

            // Deposits Applied 
            reservations.SiteDepApp = GetDepositsApplied(siteCategories, 0.0m, checkedInList);
            reservations.RentalDepApp = GetDepositsApplied(rentalCategories, 0.0m, checkedInList);
            reservations.GolfDepApp = GetDepositsApplied(golfCategories, 0.0m, checkedInList);

            // Manual Deposit Refunds For Sites, Rentals, and Golf Carts
            var manualRefunds = GetManualRefundTransactions(transactions);
            var (depositRefunds, incomeRefunds) = SplitManualRefunds(manualRefunds);

            reservations.SiteDepMRG = getRefundTotal(siteCategories, depositRefunds);
            reservations.RentalDepMRG = getRefundTotal(rentalCategories, depositRefunds);
            reservations.GolfDepMRG = getRefundTotal(golfCategories, depositRefunds);

            // START: Gift Vouchers
            reservations.VouchersPurch = GiftVouchersPurchased(transactions);
            reservations.VouchersRedSite  = GiftVouchers(transactions, siteCategories);
            reservations.VouchersRedRental = GiftVouchers(transactions, rentalCategories);
            reservations.VouchersRedSiteDep = GiftVouchersDeposit(transactions, siteCategories);
            reservations.VouchersRedRentalDep = GiftVouchersDeposit(transactions, rentalCategories);
            reservations.VouchersRedStorage = GiftVouchersStorage(transactions);
            // END: Gift Vouchers
            // --------------- END RESERVATIONS DEPOSITS (DBR) TABLE --------------------

            // --------------- START RESERVATIONS INCOME (DBF) TABLE -------------------
            reservations.Sites = await Get_Taxed_Totals(siteCategories, 0.0m, checkedInList);
            reservations.Rentals = await Get_Taxed_Totals(rentalCategories, 0.0m, checkedInList);
            reservations.Lock_Fees = await GetLockFeesForDay(startDate, depositsTakenList_NoRefunds); // Calculates lock fees from deposits from TF. 
            
            // Extra Vehicle / Visitor Fees 
            // Tried to mimic NewbookImport logic as much as possible?
            var extraVehiclesList = transactions.Where(p => p.Description != null && extrasVehicles.Any(c => p.Description.Contains(c, StringComparison.OrdinalIgnoreCase))).ToList();
            var extraVehiclesListNoVoided = IgnoreEntries("Voided Payments Voided", extraVehiclesList, transactions);
            var extraVehiclesTotal = extraVehiclesList.Where(p => p.Amount.HasValue && (p.Amount == -40m || p.Amount == -80m || p.Amount == -22.4m)).Sum(p => Math.Abs(p.Amount ?? 0));

            var extraVisitorsTotal = extraVehiclesListNoVoided.Where(p => p.Amount.HasValue && (p.Amount == -42 || p.Amount == -84)).Sum(p => Math.Abs(p.Amount ?? 0));
            var extraVisitorsTotal_WithAccom = transactions.Where(p => p.Amount.HasValue && p.Description != null && p.Description.Contains("Accommodation", StringComparison.OrdinalIgnoreCase)
            && (p.Amount == -117.6m)).Sum(p => Math.Abs(p.Amount ?? 0));
            reservations.Extra_Vehicle_Fees = extraVehiclesTotal;
            reservations.Visitor_Fees = extraVisitorsTotal + extraVisitorsTotal_WithAccom;

            // Damage Fees 
            reservations.Damage_Fees = transactions.Where(p => p.Amount.HasValue && p.Description != null && p.Description.Contains("Damage", StringComparison.OrdinalIgnoreCase)).Sum(p => Math.Abs(p.Amount ?? 0));
            // Late Fees 
            reservations.Late_Fees = transactions.Where(p => p.Amount.HasValue && p.Description != null && p.Description.Contains("Late", StringComparison.OrdinalIgnoreCase)).Sum(p => Math.Abs(p.Amount ?? 0));
            // Supplemental
            reservations.Supplemental = transactions.Where(p => p.Amount.HasValue && p.Description != null && (p.Description.Contains("Trailer Sales", StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains("Trash Pickup", StringComparison.OrdinalIgnoreCase))).Sum(p => Math.Abs(p.Amount ?? 0));

            // Golf Cart Income
            reservations.Golf_Cart_Rentals = await Get_Taxed_Totals(golfCategories, 0.0m, checkedInList);                     

            // MRG1 - Manual Refunds FROM INCOME & NOT VOIDED & LESS THAN 90 DAYS 
            var income_Refunds_Short_Term_Stays = incomeRefunds
                .Where(r => r.Amount.HasValue && r.Amount > 0 &&
                r.ArrivalDate.HasValue && r.DepartureDate.HasValue &&
                (r.DepartureDate.Value - r.ArrivalDate.Value).TotalDays < 90 &&
                r.PaymentMethod != null &&
                r.PaymentMethod.Contains("Manual", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

            reservations.MRG1 = income_Refunds_Short_Term_Stays
            .Where(r =>
                (
                    (
                        r.Category != null &&
                        (
                            siteCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                            rentalCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
                        )
                    )
                    ||
                    (
                        r.Description != null &&
                        (
                            r.Description.Contains("Late", StringComparison.OrdinalIgnoreCase) ||
                            r.Description.Contains("Lock", StringComparison.OrdinalIgnoreCase) ||
                            r.Description.Contains("Vehicle", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                )
                &&
                !(
                    (r.Category != null && r.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase)) ||
                    (r.Description != null && r.Description.Contains("Storage", StringComparison.OrdinalIgnoreCase))
                )
            )
            .Sum(r => Math.Abs(r.Amount ?? 0));

            // Annual_Leases
            reservations.Annual_Leases = GetNoTaxTotals(transactions, annualLease);

            
            // Employee
            var employeePayments = transactions
                .Where(p => p.Category != null &&
                            (employee.Any(c => p.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                            employee.Any(c => p.Description.Contains(c, StringComparison.OrdinalIgnoreCase))) &&
                            p.TransType == "Payments Raised")
                .Where(p => !transactions.Any(v =>
                    v.AccountForId == p.AccountForId &&
                    v.ItemId == p.ItemId &&
                    v.TransType == "Voided Payments Voided"))
                .Sum(p => Math.Abs(p.Amount ?? 0));

            var employeeRefunds = transactions
                .Where(p => p.Category != null &&
                            (employee.Any(c => p.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                            employee.Any(c => p.Description.Contains(c, StringComparison.OrdinalIgnoreCase))) &&
                            p.TransType == "Refunds Raised")
                .Where(p => !transactions.Any(v =>
                    v.AccountForId == p.AccountForId &&
                    v.ItemId == p.ItemId &&
                    v.TransType == "Voided Refunds Voided"))
                .Sum(p => Math.Abs(p.Amount ?? 0));

            reservations.Employee = employeePayments - employeeRefunds;

            // Long Term Sites
            reservations.LT_Sites = GetLongTermBookingTotal(startDate, siteCategories, transactions);
            // Long Term Rentals
            reservations.LT_Rentals = GetLongTermBookingTotal(startDate, rentalCategories, transactions);
            // Mobile Home Parks
            reservations.MH_Park = GetNoTaxTotals(transactions, mobileHome);

            // MRG2 Refunds - Income Refunds for Annual Leases, Employee, Long Term Stays (90+ days), and Mobile Homes.
            string[] mrg2Categories = annualLease.Concat(employee).Concat(mobileHome).ToArray();
            reservations.MRG2 = MRG2_Refunds(mrg2Categories, transactions, siteCategories, rentalCategories);

            // Storage (Income)
            var storagePayments = transactions
                .Where(p =>
                    p != null &&
                    p.TransType == "Payments Raised" &&
                    !string.IsNullOrEmpty(p.Category) &&
                    (
                        p.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains("TRAILER MOVE FEE", StringComparison.OrdinalIgnoreCase)
                    )
                ).Sum(p => Math.Abs(p.Amount ?? 0m));

            var storageRefundsBT = transactions
                .Where(p =>
                    p != null &&
                    p.TransType == "Refunds Raised" &&
                    p.TranslatedPaymentType == "Balance Transfer" &&
                    !string.IsNullOrEmpty(p.Category) &&
                    p.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase)
                ).Sum(p => p.Amount ?? 0m);

            var storageRefundsList = transactions
                .Where(p =>
                    p != null &&
                    p.TransType == "Refunds Raised" &&
                    !string.IsNullOrEmpty(p.Category) &&
                    (p.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains("Storage", StringComparison.OrdinalIgnoreCase))
                ).ToList();

            var storage_Income_Refunds_Manual = storageRefundsList.Where(s => s.PaymentMethod.Contains("Manual", StringComparison.OrdinalIgnoreCase) &&
            s.TranslatedPaymentType != "Balance Transfer" &&
            s.HasArrived == true && s.TransDate > s.BookingCheckedIn).Sum(p => p.Amount ?? 0m);

            var storageRefunds = storageRefundsList.Sum(p => p.Amount ?? 0m);
            reservations.Storage = storagePayments - storageRefundsBT - storageRefunds;

            // Transfers
            var transferFeesTotalPayments = SumCategory(transactions, "Lease Transfer", "Payments Raised", "Voided Payments Voided");
            var transferFeesTotalRefunds = SumCategory(transactions, "Lease Transfer", "Refunds Raised", "Voided Refunds Voided");
            reservations.Transfer_Fees = transferFeesTotalPayments - transferFeesTotalRefunds;

            var transfer_Income_Refunds_Manual =
            transactions
                .Where(s =>
                    s.TransType == "Refunds Raised" &&
                    s.Description?.Contains("Lease Transfer", StringComparison.OrdinalIgnoreCase) == true &&
                    s.PaymentMethod.Contains("Manual", StringComparison.OrdinalIgnoreCase) &&
                    s.TranslatedPaymentType != "Balance Transfer" &&
                    s.HasArrived == true &&
                    s.TransDate > s.BookingCheckedIn &&
                    !transactions.Any(v =>
                        v.AccountForId == s.AccountForId &&
                        v.ItemId == s.ItemId &&
                        v.TransType == "Voided Refunds Voided")
                )
                .Sum(s => s.Amount ?? 0m);

            // Misc
            var miscList = transactions
            .Where(p => p != null
                && p.TransType == "Payments Raised"
                && p.Description != null
                && (
                    p.Description.Contains("Wash", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Trash", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Taxes", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Extra", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Extras", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Washing", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Gas", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Bike", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Returned Check", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Credit Card Returned", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Wristbands", StringComparison.OrdinalIgnoreCase)
                )
                && !(
                    p.Description.Contains("Person", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Vehicle", StringComparison.OrdinalIgnoreCase) // Fixed typo
                    || p.Description.Contains("Visitor", StringComparison.OrdinalIgnoreCase)
                )
                || (p.Category != null && p.Category.Contains("Wheelchair", StringComparison.OrdinalIgnoreCase))
            )
            .ToList();

            var miscTotalRefundsList = transactions
                    .Where(p => p != null
                && p.TransType == "Refunds Raised"
                && p.Description != null
                && (
                    p.Description.Contains("Wash", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Trash", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Taxes", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Extra", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Extras", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Washing", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Gas", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Bike", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Returned Check", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Credit Card Returned", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Wristbands", StringComparison.OrdinalIgnoreCase)
                )
                && !(
                    p.Description.Contains("Person", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Vehicle", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Visitor", StringComparison.OrdinalIgnoreCase)
                )
                || (p.Category != null && p.Category.Contains("Wheelchair", StringComparison.OrdinalIgnoreCase))
            )
            .ToList();

            var misc_Income_Refunds_Manual = miscTotalRefundsList.Where(s => s.PaymentMethod.Contains("Manual", StringComparison.OrdinalIgnoreCase) &&
            s.TranslatedPaymentType != "Balance Transfer" &&
            s.HasArrived == true && s.TransDate > s.BookingCheckedIn).Sum(p => p.Amount ?? 0m);

            reservations.Misc = miscList.Sum(p => Math.Abs(p.Amount ?? 0)) - miscTotalRefundsList.Sum(p => Math.Abs(p.Amount ?? 0));

            // MRG3 - Income Refunds for Storage, Transfers, and Misc
            reservations.MRG3 = storage_Income_Refunds_Manual + transfer_Income_Refunds_Manual + misc_Income_Refunds_Manual;

            // Events
            var eventsPayments = SumCategory(transactions, "Oyster", "Payments Raised", "Voided Payments Voided");
            var eventsRefunds = SumCategory(transactions, "Oyster", "Refunds Raised", "Voided Payments Voided");
            reservations.Events = eventsPayments - eventsRefunds;

            // Propane
            var propanePayments = SumCategory(transactions, "Propane", "Payments Raised", "Voided Payments Voided");
            var propaneRefunds  = SumCategory(transactions, "Propane", "Refunds Raised", "Voided Refunds Voided");
            reservations.Propane = propanePayments - propaneRefunds;

            // Credit Card and Cash Deductions 
            var totalCreditCardDeposits = 0.0m;
            var totalCashDeposits = 0.0m;
            var totalCreditCardRefunds = 0.0m;
            var totalCashRefunds = 0.0m;


            foreach (var t in transactions)
            {
                bool isVoided = transactions.Any(v =>
                    v.ItemId == t.ItemId &&
                    v.TransType == "Voided Payments Voided");

                if (isVoided)
                    continue;

                if (t.PaymentMethod == "Authorize.Net"
                    && t.TransType == "Payments Raised"
                    && t.TranslatedPaymentType != "Balance Transfer")
                {
                    totalCreditCardDeposits += Math.Abs(t.Amount ?? 0);
                }

                else if (t.PaymentMethod == "Manual Entry"
                        && t.TransType == "Payments Raised"
                        && (t.TranslatedPaymentType == "Cash" || t.TranslatedPaymentType == "Check")
                        && t.TranslatedPaymentType != "Balance Transfer")
                {
                    totalCashDeposits += Math.Abs(t.Amount ?? 0);
                }
                else if (t.PaymentMethod == "Authorize.Net"
                        && t.TransType == "Refunds Raised"
                        && t.TranslatedPaymentType != "Balance Transfer")
                {
                    totalCreditCardRefunds += Math.Abs(t.Amount ?? 0);
                } /*
                else if (t.PaymentMethod == "Manual Entry"
                        && t.TransType == "Refunds Raised"
                        && (t.TranslatedPaymentType == "Cash" || t.TranslatedPaymentType == "Check")
                        && t.TranslatedPaymentType != "Balance Transfer")
                {
                    totalCashRefunds += Math.Abs(t.Amount ?? 0);
                } */
            }

            reservations.OfficeCC = totalCreditCardDeposits - totalCreditCardRefunds;
            reservations.OfficeCash = totalCashDeposits; //- totalCashRefunds;

            // Balance Transfers Table
            var noVoidedrefunds = IgnoreEntries("Voided Refunds Voided", manualRefunds, transactions);
            ApplyBalanceTransfers(transactions, reservations);

            // Manual Check Refunds Table
            reservations.CampsitesC = get_Refund_Checks_Income_Total(siteCategories, noVoidedrefunds);
            reservations.RentalsC = get_Refund_Checks_Income_Total(rentalCategories, noVoidedrefunds);
            reservations.GolfC = get_Refund_Checks_Income_Total(golfCategories, noVoidedrefunds);

            reservations.LTCampsitesC = noVoidedrefunds.Where(r => r.Category != null && siteCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
            && r.TranslatedPaymentType != null && r.TranslatedPaymentType.Contains("Check", StringComparison.OrdinalIgnoreCase) && (r.DepartureDate - r.ArrivalDate)?.TotalDays >= 90
            && r.HasArrived == true && r.TransDate > r.BookingCheckedIn)
            .Sum(r => Math.Abs(r.Amount ?? 0));

            reservations.LTRentalsC = noVoidedrefunds.Where(r => r.Category != null && rentalCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
            && r.TranslatedPaymentType != null && r.TranslatedPaymentType.Contains("Check", StringComparison.OrdinalIgnoreCase) && (r.DepartureDate - r.ArrivalDate)?.TotalDays >= 90
            && r.HasArrived == true && r.TransDate > r.BookingCheckedIn)
            .Sum(r => Math.Abs(r.Amount ?? 0));

            reservations.AnnualC = get_Refund_Checks_Income_Total(annualLease, noVoidedrefunds);
            reservations.MHParkC = get_Refund_Checks_Income_Total(mobileHome, noVoidedrefunds);
            reservations.StorageC = get_Refund_Checks_Income_Storage_Total(storage, noVoidedrefunds);

            reservations.SiteDepositsC = get_Refund_Checks_Deposits_Total(siteCategories, noVoidedrefunds);
            reservations.RentalDepositsC = get_Refund_Checks_Deposits_Total(rentalCategories, noVoidedrefunds);
            reservations.GolfDepositsC = get_Refund_Checks_Deposits_Total(golfCategories, noVoidedrefunds);

            reservations.OtherC = noVoidedrefunds
            .Where(r => r.Category != null &&
                        !siteCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                        !rentalCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                        !golfCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                        !annualLease.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                        !mobileHome.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                        !storage.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                        r.TranslatedPaymentType != null &&
                        r.TranslatedPaymentType.Contains("Check", StringComparison.OrdinalIgnoreCase))
            .Sum(r => Math.Abs(r.Amount ?? 0));


            reservationsList.Add(reservations);
            return reservationsList;
        }

        private async Task<List<Dictionary<string, object>>> GetCheckedInList(DateTime startDate, DateTime endDate)
        {
            var ds = await _dailyReport.RetrieveCheckInsReport(startDate, endDate);
            if (ds?.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                return ds.Tables[0].AsEnumerable()
                    .Select(row => ds.Tables[0].Columns.Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => row[col]))
                    .ToList();
            }
            return new List<Dictionary<string, object>>();
        }

        private decimal CalculateGolfCartDeposits(IEnumerable<TransactionFlow> transactions)
        {
            // Deposits (negative payments)
            var deposits = transactions
                .Where(t =>
                    t.Category?.Contains("Golf Cart", StringComparison.OrdinalIgnoreCase) == true &&
                    t.PaymentTypeAction == "Payments" &&
                    t.Amount < 0)
                .Sum(t => Math.Abs(t.Amount ?? 0));

            // Refunds (positive amounts, specific payment method)
            var refunds = transactions
                .Where(t =>
                    t.Category?.Contains("Golf Cart", StringComparison.OrdinalIgnoreCase) == true &&
                    t.PaymentTypeAction == "Refunds" &&
                    t.PaymentMethod == "Authorize.Net" &&
                    t.Amount > 0)
                .Sum(t => -(t.Amount ?? 0));

            return deposits + refunds;
        }
        
        // ----------------------------------------------
        // Returns all manual refund transactions
        // ----------------------------------------------
        private List<TransactionFlow> GetManualRefundTransactions(List<TransactionFlow> transactions)
        {
            var manualRefunds = transactions
                .Where(t =>
                    t.TransType?.Contains("Refunds Raised", StringComparison.OrdinalIgnoreCase) == true &&
                    t.TranslatedPaymentType?.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase) == false &&
                    t.PaymentMethod?.Contains("Manual", StringComparison.OrdinalIgnoreCase) == true &&
                    t.Amount.HasValue && t.Amount > 0 &&
                    t.ArrivalDate.HasValue &&
                    t.DepartureDate.HasValue &&
                    DateTime.TryParse(Convert.ToString(t.TransDate), out _)
                )
                .ToList();

            // Apply ignore logic here
            var noVoidedManualRefunds = IgnoreEntries(
                "Voided Refunds Voided",
                manualRefunds,
                transactions
            );

            return noVoidedManualRefunds.ToList();
        }

        // Splits manual refunds into deposit refunds vs income refunds
        private (List<TransactionFlow> DepositRefunds, List<TransactionFlow> IncomeRefunds) SplitManualRefunds(List<TransactionFlow> refundTransactions)
        {
            var depositRefunds = new List<TransactionFlow>();
            var incomeRefunds  = new List<TransactionFlow>();

            foreach (var r in refundTransactions)
            {
                if (!r.ArrivalDate.HasValue || !r.DepartureDate.HasValue)
                    continue;

                bool depositRefund = false;
                bool incomeRefund  = false;

                // Refund before arrival → always deposit
                if (r.TransDate < r.ArrivalDate)
                {
                    depositRefund = true;
                }
                else
                {
                    // Refund after arrival
                    bool arrivedAndCheckedIn =
                        r.HasArrived == true &&
                        r.BookingCheckedIn.HasValue &&
                        r.TransDate > r.BookingCheckedIn;

                    if (arrivedAndCheckedIn)
                    {
                        incomeRefund = true;
                    }
                    else
                    {
                        depositRefund = true;
                    }
                }

                // Long-term stay rule (90+ days)
                if ((r.DepartureDate - r.ArrivalDate)?.TotalDays >= 90)
                {
                    bool arrivedAndCheckedIn =
                        r.HasArrived == true &&
                        r.BookingCheckedIn.HasValue &&
                        r.TransDate > r.BookingCheckedIn;

                    if (arrivedAndCheckedIn)
                    {
                        depositRefund = false;
                        incomeRefund = true;
                    }
                }

                if (depositRefund && !incomeRefund)
                    depositRefunds.Add(r);

                if (incomeRefund && !depositRefund)
                    incomeRefunds.Add(r);
            }

            return (depositRefunds, incomeRefunds);
        }



        // Checks if a category matches any of the provided keywords (case-insensitive).
        // Used in the `FilterTransactions` method
        private bool MatchesCategory(string category, params string[] keywords) =>
            keywords.Any(keyword => category.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        // Checks if a deposit string contains "1".
        // Used in the `FilterTransactions` method.
        private bool MatchesDeposit(string deposit) =>
            deposit != null && deposit.Contains("1", StringComparison.OrdinalIgnoreCase);

        // Checks if a description does not contain "EXTRA VEHICLE" (case-insensitive).
        // Used in the `FilterTransactions` method.
        private bool MatchesDescription(string description) =>
            description == null || !description.Contains("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase);

        // Filters transactions based on category, deposit, and description.
        // Used in the `ProcessReservationsAsync` method to filter confirmed site and rental deposits.
        private IEnumerable<TransactionFlow> FilterTransactions(IEnumerable<TransactionFlow> transactions, string[] categories)
        {
            return transactions.Where(p =>
                p.Category != null &&
                MatchesCategory(p.Category, categories) &&
                MatchesDeposit(p.Deposit) &&
                MatchesDescription(p.Description));
        }

        // Calculates the total deposits applied for specific categories from the checked-in list.
        // Used in the `ProcessReservationsAsync` method to calculate deposits applied for sites and rentals.
        private decimal GetDepositsApplied(string[] categories, decimal total, List<Dictionary<string, object>> checkedInList)
        {
            if (checkedInList != null && checkedInList.Count > 0)
            {
                foreach (var record in checkedInList)
                {
                    if (!record.TryGetValue("Site", out var catVal) || catVal == null || catVal == DBNull.Value)
                        continue;

                    string str = Convert.ToString(catVal);
                    if (string.IsNullOrWhiteSpace(str))
                        continue;

                    bool isCategory = categories.Any(c => str.Contains(c, StringComparison.OrdinalIgnoreCase))
                    && !str.Contains("Storage", StringComparison.OrdinalIgnoreCase);
                    if (!isCategory)
                        continue;

                    if (record.TryGetValue("DepositsHeld", out var depVal) &&
                        depVal != null &&
                        depVal != DBNull.Value &&
                        decimal.TryParse(Convert.ToString(depVal), out decimal depositHeld))
                    {
                        total += Math.Abs(depositHeld);
                    }
                }
            }
            return total;
        }
        
        // Retrieves gift voucher transactions where the description contains "Gift Voucher Payment".
        // Used in the `ProcessReservationsAsync` method to calculate gift voucher purchases.
        private decimal GiftVouchersPurchased(IEnumerable<TransactionFlow> transactions)
        {
            return transactions
            .Where(r => (string.IsNullOrWhiteSpace(r.Category))
            && r.Description.Contains("Gift Voucher Payment", StringComparison.OrdinalIgnoreCase)).Sum(p => Math.Abs(p.Amount ?? 0));
        }

        // Retrieves gift voucher transactions for specific categories where the description contains "Gift".
        // Used in the `ProcessReservationsAsync` method to calculate gift vouchers for sites and rentals.
        private decimal GiftVouchers(IEnumerable<TransactionFlow> transactions, IEnumerable<string> categories)
        {
            return transactions
                .Where(r =>
                    !string.IsNullOrWhiteSpace(r.Category) &&
                    categories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                    r.Description.Contains("Gift", StringComparison.OrdinalIgnoreCase) &&
                    (r.Deposit != "1")
                )
                .Sum(p => Math.Abs(p.Amount ?? 0));
        }

        // Retrieves gift voucher deposit transactions for specific categories where the description contains "Gift".
        // Used in the `ProcessReservationsAsync` method to calculate gift voucher deposits for sites and rentals.
        private decimal GiftVouchersDeposit(IEnumerable<TransactionFlow> transactions, IEnumerable<string> categories)
        {
            return transactions
                .Where(r =>
                    r.Deposit == "1" &&
                    !string.IsNullOrWhiteSpace(r.Category) &&
                    categories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                    r.Description.Contains("Gift", StringComparison.OrdinalIgnoreCase)
                )
                .Sum(p => Math.Abs(p.Amount ?? 0));
        }
        // Retrieves gift voucher transactions for storage categories.
        // Used in the `ProcessReservationsAsync` method to calculate gift vouchers for storage.
        private decimal GiftVouchersStorage(IEnumerable<TransactionFlow> transactions)
        {
            return transactions
                .Where(r =>
                    // STORAGE CATEGORY RULES
                    !string.IsNullOrWhiteSpace(r.Category) &&
                    !r.Category.Equals("GUEST", StringComparison.OrdinalIgnoreCase) &&
                    (
                        r.Category.StartsWith("STORAGE", StringComparison.OrdinalIgnoreCase) ||
                        r.Category.Equals("FRONT PARKING LOT", StringComparison.OrdinalIgnoreCase)
                    )
                    &&

                    r.Description != null &&
                    r.Description.Contains("Gift", StringComparison.OrdinalIgnoreCase) &&

                    // BALANCE TRANSFER RULES
                    (
                        // FROM CLIENT or BALANCE TRANSFER
                        r.Description.Contains("FOR GIFT VOUCHER FROM CLIENT", StringComparison.OrdinalIgnoreCase) ||
                        r.Description.Contains("BALANCE TRANSFER TO ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
                        r.Description.Contains("BALANCE TRANSFER FROM ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
                        r.Description.Contains("BALANCE TRANSFER TO CLIENT ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
                        r.Description.Contains("BALANCE TRANSFER FROM CLIENT ACCOUNT", StringComparison.OrdinalIgnoreCase) ||

                        // TO CLIENT (reverse in legacy)
                        r.Description.Contains("FOR GIFT VOUCHER TO CLIENT", StringComparison.OrdinalIgnoreCase)
                    )
                )
                .Sum(p => Math.Abs(p.Amount ?? 0));
        }

        // Retrieves the total lock fees for a specific day from the Bookings Table in the database.
        // Used in the `ProcessReservationAsync` method to calculate lock fees for the day.
         private async Task<decimal> GetLockFeesForDay(DateTime date, List<TransactionFlow> depositsTakenList_NoRefunds)
        {
            decimal total = 0m;

            // Filter relevant transactions
            var todaysTx = depositsTakenList_NoRefunds
                .Where(t => t.TransDate.Date == date.Date)
                .ToList();

            if (!todaysTx.Any())
                return 0m;

            var bookingIds = todaysTx.Select(t => t.AccountForId).Distinct().ToList();

            var sqlParamNames = bookingIds.Select((_, i) => $"@id{i}").ToList();

            string sql = $@"
                SELECT BookingId, LockFee
                FROM dbo.Bookings
                WHERE BookingId IN ({string.Join(",", sqlParamNames)});
            ";

            Dictionary<int, decimal> dbLockFees = new Dictionary<int, decimal>();

            using (var sqlConn = _dbConnectionService.CreateConnection())
            using (var cmd = new SqlCommand(sql, sqlConn))
            {
                // Add parameters
                for (int i = 0; i < bookingIds.Count; i++)
                    cmd.Parameters.AddWithValue(sqlParamNames[i], bookingIds[i]);

                await sqlConn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        decimal lockFee = reader.IsDBNull(1) ? 0 : Math.Abs(reader.GetDecimal(1));
                        dbLockFees[id] = lockFee;

                        total += lockFee;
                    }
                }
            }

            // Handle bookingIds NOT found in DB  
            foreach (var tx in todaysTx)
            {
                if (!dbLockFees.ContainsKey(tx.AccountForId))
                {
                    // Apply fallback rules based on Tx amount
                    var amount = Math.Abs(tx.Amount ?? 0);

                    if (amount == 40 || amount == 140 || amount == 240)
                        total += 40;
                    else if (amount == 30 || amount == 130 || amount == 230)
                        total += 30;
                }
            }

            return total;
        }
        // Retrieves Sites, Rentals, and Golf Carts amount earned as Income
       private async Task<Decimal> Get_Taxed_Totals(string[] categories, decimal total, List<Dictionary<string, object>>? checkedInList)
        {
            if (checkedInList != null && checkedInList.Count > 0)
            {
                foreach (var record in checkedInList)
                {
                    if (!record.TryGetValue("BookingArrival", out var arrivalVal) || arrivalVal == null || arrivalVal == DBNull.Value)
                        continue;
                    if (!record.TryGetValue("BookingDeparture", out var departureVal) || departureVal == null || departureVal == DBNull.Value)
                        continue;

                    if (!record.TryGetValue("Site", out var catVal) || catVal == null || catVal == DBNull.Value)
                        continue;

                    string str = Convert.ToString(catVal);
                    if (string.IsNullOrWhiteSpace(str))
                        continue;

                    if (!DateTime.TryParse(Convert.ToString(arrivalVal), out DateTime arrivalDate))
                        continue;
                    if (!DateTime.TryParse(Convert.ToString(departureVal), out DateTime departureDate))
                        continue;

                    bool isSiteCategory = categories.Any(c =>
                        str.Contains(c, StringComparison.OrdinalIgnoreCase))
                        && !str.Contains("Storage", StringComparison.OrdinalIgnoreCase)
                        && !((departureDate - arrivalDate).TotalDays >= 90);

                    if (!isSiteCategory)
                        continue;

                    decimal depositHeld = 0m;
                    decimal paymentsAfter = 0m;
                    decimal securityDeposit = 0m;

                    if (record.TryGetValue("DepositsHeld", out var depVal) && depVal != null && depVal != DBNull.Value)
                        decimal.TryParse(Convert.ToString(depVal), out depositHeld);

                    if (record.TryGetValue("PaymentsAfterCheckIn", out var afterVal) && afterVal != null && afterVal != DBNull.Value)
                        decimal.TryParse(Convert.ToString(afterVal), out paymentsAfter);
                    
                    if (record.TryGetValue("SecurityDeposits", out var secVal) && secVal != null && secVal != DBNull.Value)
                        decimal.TryParse(Convert.ToString(secVal), out securityDeposit);

                    if (depositHeld != 0 || paymentsAfter != 0)
                    {
                        decimal totalForSite = depositHeld + paymentsAfter;
                        total += Math.Abs(totalForSite);
                        if (securityDeposit != 0)
                        {
                            total -= Math.Abs(securityDeposit);
                        }
                    }
                 
                }
            }
            return total;
        }


        // Calculates the totals for Annual Leases and MH Parks from the TF list
        private decimal GetNoTaxTotals(IEnumerable<TransactionFlow> transactions, string[] categories)
        {
            decimal total = 0m;

            var annualLeaseTransactions = transactions
             .Where(t =>
                 t.Category != null &&
                 categories.Any(c => t.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                 t.Description != null &&
                 !t.Description.Contains("Vehicle", StringComparison.OrdinalIgnoreCase) &&
                 !t.Description.Contains("Transfer", StringComparison.OrdinalIgnoreCase) &&
                 !t.Description.Contains("Employee", StringComparison.OrdinalIgnoreCase))
             .ToList();

            foreach (var t in annualLeaseTransactions)
            {
                total += Math.Abs(t.Amount ?? 0m);
            }

            return total;
        }


        private decimal GetLongTermBookingTotal(DateTime transDate, string[] categories, List<TransactionFlow> transactions)
        {
            decimal sqlTotal = 0m;
            decimal transactionTotal = 0m;

            var likeConditions = new List<string>();
            for (int i = 0; i < categories.Length; i++)
                likeConditions.Add($"SITE LIKE @cat{i}");

            string categoryFilter = string.Join(" OR ", likeConditions);

            string sqlQuery = $@"
                SELECT SUM(DepositsHeld + PaymentsAfterCheckIn)
                FROM dbo.CheckedIn
                WHERE CAST(BookingCheckedIn AS date) = @transDate
                AND ({categoryFilter})
                AND SITE NOT LIKE '%Storage%'
                AND DATEDIFF(DAY, BookingArrival, BookingDeparture) >= 90;
            ";

            using (var sqlConn = _dbConnectionService.CreateConnection())
            using (var cmd = new SqlCommand(sqlQuery, sqlConn))
            {
                cmd.Parameters.Add("@transDate", SqlDbType.Date).Value = transDate.Date;

                // Add category parameters
                for (int i = 0; i < categories.Length; i++)
                    cmd.Parameters.Add($"@cat{i}", SqlDbType.NVarChar).Value = "%" + categories[i] + "%";

                sqlConn.Open();
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    sqlTotal = Math.Abs(Convert.ToDecimal(result));
            }

            foreach (var t in transactions)
            {
                // EXCLUDE balance transfers
                if (t.Description != null &&
                    t.Description.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isLongTerm = false;

                if (t.ArrivalDate.HasValue && t.DepartureDate.HasValue)
                {
                    isLongTerm = (t.DepartureDate.Value - t.ArrivalDate.Value).TotalDays >= 90;
                }

                bool categoryMatch =
                    categories.Any(cat =>
                        !string.IsNullOrEmpty(t.Category) &&
                        t.Category.Contains(cat, StringComparison.OrdinalIgnoreCase)
                    ) &&
                    !t.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(t.Description) &&
                    (
                        t.Description.Contains("accommodation", StringComparison.OrdinalIgnoreCase) ||
                        t.Description.Contains("accomodation", StringComparison.OrdinalIgnoreCase)
                    );


                if (isLongTerm && categoryMatch && t.HasArrived == true && t.TransDate > t.BookingCheckedIn)
                {
                    transactionTotal += Math.Abs(t.Amount ?? 0);
                }

            }

            return sqlTotal + transactionTotal;
        }

        private static decimal SumCategory(IEnumerable<TransactionFlow> transactions, string categoryText, string transType, string voidedType)
        {
            return transactions
                .Where(p =>
                    p?.TransType == transType &&
                    p.Description?.Contains(categoryText, StringComparison.OrdinalIgnoreCase) == true &&
                    !transactions.Any(v =>
                        v.AccountForId == p.AccountForId &&
                        v.ItemId == p.ItemId &&
                        v.TransType == voidedType)
                )
                .Sum(p => Math.Abs(p.Amount ?? 0));
        }
        
        private static List<TransactionFlow> GetCategoryList(IEnumerable<TransactionFlow> transactions,string categoryText,string transType,string voidedType)
        {
            return transactions
                .Where(p =>
                    p?.TransType == transType &&
                    p.Description?.Contains(categoryText, StringComparison.OrdinalIgnoreCase) == true &&
                    !transactions.Any(v =>
                        v.AccountForId == p.AccountForId &&
                        v.ItemId == p.ItemId &&
                        v.TransType == voidedType)
                )
                .ToList();
        }


        private void ApplyBalanceTransfers(List<TransactionFlow> transactions, Reservations deposits)
        {
            _bucketTotals.Clear();
            foreach (var t in transactions)
            {
                if (!IsBalanceTransfer(t))
                    continue;

                decimal amount = t.Amount ?? 0;
                string bucket = ResolveBalanceTransferBucket(t, transactions);

                if (bucket != null)
                {
                    if (!_bucketTotals.ContainsKey(bucket)) _bucketTotals[bucket] = 0; _bucketTotals[bucket] += amount;
                }
                ApplyToDeposits(bucket, amount, deposits);
            }
        }

        private bool IsBalanceTransfer(TransactionFlow t)
        {
            if (t == null)
                return false;

            if (t.TranslatedPaymentType?.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase) ?? false)
                return true;

            if (t.Description?.Contains("BALANCE TRANSFER", StringComparison.OrdinalIgnoreCase) ?? false)
                return true;

            if (t.Description?.Contains("TRANSFER FROM", StringComparison.OrdinalIgnoreCase) ?? false)
                return true;

            if (t.Description?.Contains("TRANSFER TO", StringComparison.OrdinalIgnoreCase) ?? false)
                return true;
            

            return false;
        }

        private string ResolveBalanceTransferBucket(TransactionFlow t, List<TransactionFlow> transactions)
        {

            // 2. BALANCE TRANSFER LOGIC 
            if (t.TranslatedPaymentType?.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase) == true)
            {
                // TRUE VOIDED pair
                bool isVoided = transactions.Any(x =>
                    (
                        x.PaymentTypeReference == t.ItemId ||
                        x.ItemId == t.PaymentTypeReference
                    ) &&
                    (x.TransType.Contains("Voided Payments Voided", StringComparison.OrdinalIgnoreCase) || x.TransType.Contains("Voided Refunds Voided", StringComparison.OrdinalIgnoreCase)));

                if (isVoided)
                    return null;
            }

            string cat = t.Category?.ToUpper() ?? "";
            string desc = t.Description?.ToUpper() ?? "";
            string client = t.ClientAccount?.ToUpper() ?? "";

            if (Math.Abs(t.Amount ?? 0) == 40 || Math.Abs(t.Amount ?? 0) == 30)
                    return null;

            // Splits
            //  - Find the paired transaction to get category
            if (client.Contains("SPLIT"))
            {

                // Find the corresponding parent transaction
                var pairedTransaction = transactions.FirstOrDefault(x =>
                    x.ItemId == t.PaymentTypeReference || 
                    x.PaymentTypeReference == t.ItemId);

                if (pairedTransaction != null)
                {
                    
                    // Use the parent's category to determine bucket
                    string pairedCat = pairedTransaction.Category?.ToUpper() ?? "";
                    
                    // Determine if it's a site or rental based on parent category
                    if (pairedCat.Contains("WESC") || pairedCat.Contains("WATER & ELECTRIC"))
                        return "CampsitesT";
                        
                    else if (pairedCat.Contains("TRAVEL TRAILER") || pairedCat.Contains("COTTAGE") || 
                            pairedCat.Contains("CABIN") || pairedCat.Contains("VILLA"))
                        return "RentalsT";
                    
                    else if (pairedCat.Contains("GOLF"))
                        return "GolfCarts";
                }
            }

            if (t.Description?.Contains("GIFT", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (cat.Contains("WESC") || cat.Contains("WATER & ELECTRIC ONLY", StringComparison.OrdinalIgnoreCase))
                {
                    if (t.HasArrived == true && t.TransDate > t.BookingCheckedIn)
                        return "Vouchers";
                    else
                        return "SiteDepositsT";
                }
                else if (cat.Contains("Rental") || cat.Contains("Rentals", StringComparison.OrdinalIgnoreCase) &&
                !t.ClientAccount.Contains("STORAGE", StringComparison.OrdinalIgnoreCase))
                {
                    if (t.HasArrived == true && t.TransDate > t.BookingCheckedIn)
                        return "Vouchers";
                    else
                        return "RentalsT";
                }
                return "Vouchers";
            }
                
            //Forfeit
            if (t.AccountForId == 110372 || t.AccountForId == 110373)
            {
                // SITES / WESC
                if (t.ClientAccount.Contains("Sites") || t.ClientAccount.Contains("Site", StringComparison.OrdinalIgnoreCase))
                {
                    return "CampsitesT";
                }
                if ((t.ClientAccount.Contains("Rental", StringComparison.OrdinalIgnoreCase) || t.ClientAccount.Contains("Rentals", StringComparison.OrdinalIgnoreCase)) &&
                !t.ClientAccount.Contains("STORAGE", StringComparison.OrdinalIgnoreCase))
                {
                    return "RentalsT";
                }
            }

            // SITES / WESC
            if (cat.Contains("WESC") || cat.Contains("WATER & ELECTRIC ONLY", StringComparison.OrdinalIgnoreCase))
            {

                if (t.HasArrived == true && t.TransDate > t.BookingCheckedIn)
                    return "CampsitesT";

                return "SiteDepositsT";
            }

            // RENTALS
            if ((cat.Contains("OCEAN", StringComparison.OrdinalIgnoreCase) || cat.Contains("COTTAGE", StringComparison.OrdinalIgnoreCase) ||
                cat.Contains("CABIN", StringComparison.OrdinalIgnoreCase) || cat.Contains("TRAILER", StringComparison.OrdinalIgnoreCase)) &&
                !cat.Contains("STORAGE", StringComparison.OrdinalIgnoreCase))
            {
                if (t.HasArrived == true && t.TransDate > t.BookingCheckedIn)
                    return "RentalsT";

                return "RentalDepositsT";
            }

            // GOLF
            if (cat.Contains("GOLF"))
            {
                if (t.HasArrived == true && t.TransDate > t.BookingCheckedIn)
                    return "GolfCarts";

                return "GolfDepositsT";
            }

            // ANNUAL
            if (cat.Contains("ANNUAL"))
                return "AnnualT";

            // MOBILE HOMES
            if (cat.Contains("MOBILE") || cat.Contains("M/H") || cat.Contains("MH"))
                return "MHParkT";

            // STORAGE
            if (cat.Contains("STORAGE"))
                return "StorageT";

            // GUESTS
            if (cat.Contains("GUEST"))
                return "Guests";

            // fallback
            return "Other";
        }


        private void ApplyToDeposits(string bucket, decimal amount, Reservations d)
        {
            decimal flipped = amount * -1;

            switch (bucket)
            {
                case "CampsitesT": d.CampsitesT += flipped; break;
                case "RentalsT": d.RentalsT += flipped; break;
                case "GolfCarts": d.GolfCarts += flipped; break;
                case "AnnualT": d.AnnualT += flipped; break;
                case "MHParkT": d.MHParkT += flipped; break;
                case "StorageT": d.StorageT += flipped; break;
                case "SiteDepositsT": d.SiteDepositsT += flipped; break;
                case "RentalDepositsT": d.RentalDepositsT += flipped; break;
                case "GolfDepositsT": d.GolfDepositsT += flipped; break;
                case "Vouchers": d.Vouchers += flipped; break;
                case "Forfeits": d.Forfeits += flipped; break;
                case "Guests": d.Guests += flipped; break;
                default: d.Other += flipped; break;
            }
        }

        private List<TransactionFlow> IgnoreEntries(string TransType, List<TransactionFlow> listType, List<TransactionFlow> transactions)
        {
            var noVoids = listType.Where(p =>
            {
                if (string.IsNullOrEmpty(p.PaymentTypeReference))
                    return true;

                bool hasRefund = transactions.Any(r =>
                    r.AccountForId == p.AccountForId &&
                    r.PaymentTypeReference == p.PaymentTypeReference &&
                    (r.TransType == TransType ||
                    r.TransType == "Voided Refunds Voided" ||
                    r.TransType == "Voided Payments Voided") &&
                    (r.Amount ?? 0) == Math.Abs(p.Amount ?? 0)
                );

                return !hasRefund;
            }).ToList();

            return noVoids;
        }

        private decimal getRefundTotal(string[] categories, List<TransactionFlow> noVoidedrefunds)
        {
            var refundTotal = noVoidedrefunds.Where(r => r.Category != null && categories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) && !r.Category.Contains("Storage")
            && r.TranslatedPaymentType != null && !r.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
            .Sum(r => Math.Abs(r.Amount ?? 0));

            return refundTotal;
        }

        private decimal get_Refund_Checks_Income_Total(string[] categories, List<TransactionFlow> noVoidedrefunds)
        {
            var refundChecks_Total = noVoidedrefunds.Where(r => r.Category != null && categories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
            && r.TranslatedPaymentType != null && r.TranslatedPaymentType.Contains("Check", StringComparison.OrdinalIgnoreCase)
            && r.PaymentTypeAction.Contains("Refunds", StringComparison.OrdinalIgnoreCase) &&
            !r.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
            r.HasArrived == true &&
            r.TransDate > r.BookingCheckedIn)
            .Sum(r => Math.Abs(r.Amount ?? 0));

            return refundChecks_Total;
        }

        private decimal get_Refund_Checks_Income_Storage_Total(string[] categories, List<TransactionFlow> noVoidedrefunds)
        {
            var refundChecks_Total = noVoidedrefunds.Where(r => r.Category != null && categories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
            && r.TranslatedPaymentType != null && r.TranslatedPaymentType.Contains("Check", StringComparison.OrdinalIgnoreCase)
            && r.PaymentTypeAction.Contains("Refunds", StringComparison.OrdinalIgnoreCase))
            .Sum(r => Math.Abs(r.Amount ?? 0));

            return refundChecks_Total;
        }

        private decimal get_Refund_Checks_Deposits_Total(string[] categories, List<TransactionFlow> noVoidedrefunds)
        {
            var refundDepositsChecks_Total = noVoidedrefunds.Where(r => r.Category != null && categories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
            && r.TranslatedPaymentType != null && r.TranslatedPaymentType.Contains("Check", StringComparison.OrdinalIgnoreCase)
            && r.PaymentTypeAction.Contains("Refunds", StringComparison.OrdinalIgnoreCase) &&
            !r.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
            r.HasArrived == false)
            .Sum(r => Math.Abs(r.Amount ?? 0));

            return refundDepositsChecks_Total;
        }
        
        // Calculates income refunds for manual-entry payments for Employee, Annual Leases, Long Term Stays (90+ days), and Mobile Homes.
        // Filters out voided refunds.
        private decimal MRG2_Refunds(string[] categories, List<TransactionFlow> transactions, string[] siteCategories, string[] rentalCategories)
        {
            decimal longTermSitesRefundsNT = 0m;
            decimal longTermRentalsRefundsNT = 0m;

            // Base filter for Manual Income Refunds (regardless of stay length)
            var refundCandidates = transactions
                .Where(p =>
                    p.Category != null &&
                    (categories.Any(c => p.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                    categories.Any(c => p.Description?.Contains(c, StringComparison.OrdinalIgnoreCase) == true)) &&

                    !p.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&

                    p.TransType == "Refunds Raised" &&
                    p.PaymentMethod?.Contains("Manual", StringComparison.OrdinalIgnoreCase) == true &&
                    p.TranslatedPaymentType != "Balance Transfer" &&
                    p.Amount.HasValue && p.Amount > 0 &&

                    // Income refund rules:
                    p.HasArrived == true &&
                    p.TransDate > p.BookingCheckedIn
                )
                // Exclude voided refunds
                .Where(p => !transactions.Any(v =>
                    v.AccountForId == p.AccountForId &&
                    v.ItemId == p.ItemId &&
                    v.TransType == "Voided Refunds Voided"))
                .ToList();

            // Handle LONG-TERM manual income refunds (90+ days)
            var longTermRefunds = refundCandidates
                .Where(t =>
                    t.ArrivalDate.HasValue &&
                    t.DepartureDate.HasValue &&
                    (t.DepartureDate.Value - t.ArrivalDate.Value).TotalDays >= 90 &&
                    (siteCategories.Any(c => t.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                    rentalCategories.Any(c => t.Category.Contains(c, StringComparison.OrdinalIgnoreCase)))
                )
                // Exclude voided LONG-TERM payments
                .Where(t => !transactions.Any(v =>
                    v.AccountForId == t.AccountForId &&
                    v.ItemId == t.ItemId &&
                    v.TransType == "Voided Payments Voided"))
                .ToList();

            foreach (var refund in longTermRefunds)
            {
                if (refund.HasArrived == true && refund.TransDate > refund.BookingCheckedIn)
                {
                    if (siteCategories.Any(c => refund.Category.Contains(c, StringComparison.OrdinalIgnoreCase)))
                        longTermSitesRefundsNT += Math.Abs(refund.Amount ?? 0);

                    else if (rentalCategories.Any(c => refund.Category.Contains(c, StringComparison.OrdinalIgnoreCase)))
                        longTermRentalsRefundsNT += Math.Abs(refund.Amount ?? 0);
                }
            }

            // Short-term income refunds (basic rule set)
            // Exclude the long-term ones so they aren’t double-counted.
            var regularIncomeRefunds = refundCandidates
                .Where(p =>
                    !(p.ArrivalDate.HasValue && p.DepartureDate.HasValue &&
                    (p.DepartureDate.Value - p.ArrivalDate.Value).TotalDays >= 90))
                .Sum(p => Math.Abs(p.Amount ?? 0));

            // Total manual income refunds
            return regularIncomeRefunds + longTermSitesRefundsNT + longTermRentalsRefundsNT;
        }
    }
}