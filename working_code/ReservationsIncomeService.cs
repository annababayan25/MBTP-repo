using System.Data;
using MBTP.Models;
using Newtonsoft.Json;
using MBTP.Retrieval;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.Json;
using MBTP.Interfaces;
using Microsoft.Data.SqlClient;

namespace MBTP.Services
{
    public class ReservationsIncomeService : NewbookBaseApi
    {
        private readonly TransactionFlowApi _transactionFlowApi;
        private readonly DailyReport _dailyReport;
        private readonly PaymentsApi _paymentsApi;
        private readonly ReconApi _reconApi;
        private readonly IDatabaseConnectionService _dbConnectionService;

        public ReservationsIncomeService(IDatabaseConnectionService dbConnectionService, HttpClient client, PaymentsApi paymentsApi, ReconApi reconApi, DailyReport dailyReport, TransactionFlowApi transactionFlowApi)
            : base(client)
        {
            _dbConnectionService = dbConnectionService;
            _transactionFlowApi = transactionFlowApi;
            _dailyReport = dailyReport;
            _paymentsApi = paymentsApi;
            _reconApi = reconApi;
        }

        public async Task<List<ReservationsDeposits>> ProcessReservationsIncomeAsync(DateTime startDate, DateTime endDate)
        {
            // Call the Transaction Flow Api and retrieve the list
            var transactions = await _transactionFlowApi.PopulateTransactions(startDate, endDate);

            //START: Retrieve the Checked In List DataSet and convert it to a list
            DataSet ds = await _dailyReport.RetrieveCheckInsReport(startDate, endDate);

            List<Dictionary<string, object>> checkedInList = new();

            if (ds != null && ds.Tables.Count > 0)
            {
                DataTable table = ds.Tables[0];
                if (table.Rows.Count > 0)
                {
                    checkedInList = table.AsEnumerable().Select(row => table.Columns.Cast<DataColumn>().ToDictionary(col => col.ColumnName, col => row[col])).ToList();
                }
            }

             string[] siteCategories = { "WESC", "Water & Electric Only" };
            string[] rentalCategories = { "Ocean Villa", "Cottage", "Cabin", "Travel Trailer" };
            string[] extrasVehicles = { "Extra Vehicle", "Vehicle", "Vehicles", "Extra Vehicles Fee", "Extra Vehicle Fees", "Extra" };
            string[] annualLease = {"Annual", "Annual Lease", "ANNUAL LEASE", "A/L"};
            string[] employee = {"Employee"};
            string[] mobileHome = {"Mobile", "Mobile Home", "M/L", "ML"};
            string[] storage = {"Storage"};
            string[] security = {"Security"};

            //START: Fetch Sites data for DBF and Daily Reservations Table
            decimal sitesTotal = 0m;
            sitesTotal = await GetTotals(siteCategories, sitesTotal, checkedInList);
            //END: Fetch Sites data for DBF and Daily Reservations Table

            //START: Fetch Rental data for DBF and Daily Reservations Table
            decimal rentalsTotal = 0m;
            rentalsTotal = await GetTotals(rentalCategories, rentalsTotal, checkedInList);
            //END: Fetch Rental data for DBF and Daily Reservations Table

            // Filter all transactions related to extra vehicles
            var extraVehiclesList = transactions.Where(p => p.Description != null && extrasVehicles.Any(c => p.Description.Contains(c, StringComparison.OrdinalIgnoreCase))).ToList();
            var extraVehiclesListNoVoided = extraVehiclesList.Where(p =>
            {
                if (string.IsNullOrEmpty(p.PaymentTypeReference) || p.AccountForId == null)
                    return true;

                bool hasVoided = transactions.Any(r =>
                    r.AccountForId == p.AccountForId &&
                    r.ItemId == p.ItemId &&
                    r.TransType == "Voided Payments Voided" || r.TransType == "Voided Refunds Voided");
                
                return !hasVoided;
            }).ToList();
            var extraVisitorsTotal = extraVehiclesListNoVoided.Where(p => p.Amount.HasValue && (p.Amount == -42 || p.Amount == -84)).Sum(p => Math.Abs(p.Amount.Value));
            var extraVisitorsTotalWithAccom = transactions.Where(p => p.Amount.HasValue && p.Description != null && p.Description.Contains("Accommodation", StringComparison.OrdinalIgnoreCase)
            && (p.Amount == -117.6m)).Sum(p => Math.Abs(p.Amount.Value));

            var extraVehiclesTotal = extraVehiclesList.Where(p => p.Amount.HasValue && (p.Amount == -40m || p.Amount == -80m || p.Amount == -22.4m)).Sum(p => Math.Abs(p.Amount.Value));

            bool hasMatchingDate = transactions.Any(p =>
            {
                if (p.TransDate == null) return false;
                var dateValue = Convert.ToDateTime(p.TransDate);
                return dateValue.Date == startDate.Date;
            });
            var incomeRefunds = new List<TransactionFlow>();

            var noVoidedrefundsIncome = incomeRefunds.Where(p =>
            {
                if (string.IsNullOrEmpty(p.PaymentTypeReference) || p.AccountForId == null)
                    return true;

                bool hasRefund = transactions.Any(r =>
                    r.AccountForId == p.AccountForId &&
                    r.ItemId == p.ItemId &&
                    r.TransType == "Voided Refunds Voided");
                return !hasRefund;
            }).ToList();

            // Manual Refunds FROM INCOME & NOT VOIDED & LESS THAN 90 DAYS
            var incomeRefundsFinal = noVoidedrefundsIncome
                .Where(r => r.Amount.HasValue && r.Amount > 0 &&
                r.ArrivalDate.HasValue && r.DepartureDate.HasValue &&
                (r.DepartureDate.Value - r.ArrivalDate.Value).TotalDays < 90 &&
                r.PaymentMethod != null &&
                r.PaymentMethod.Contains("Manual", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();



            var incomeRefundsSitesAndRentals = incomeRefundsFinal
                .Where(r => r.Category != null &&
                            (siteCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                            rentalCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase))) ||
                            (r.Description != null && r.Description.Contains("Late", StringComparison.OrdinalIgnoreCase)
                            || r.Description.Contains("Lock", StringComparison.OrdinalIgnoreCase))
                            || r.Description.Contains("Vehicle", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var ite in incomeRefundsSitesAndRentals)
            {
                Console.WriteLine($"incomeRefundsSitesAndRentals: BookingId: {ite.AccountForId}, Amount: {ite.Amount}, TransType: {ite.TransType}, PaymentMethod: {ite.PaymentMethod}, Category: {ite.Category}");
            }

            var incomeRefundsSitesAndRentalsTotal = incomeRefundsSitesAndRentals.Sum(r => Math.Abs(r.Amount ?? 0));

            var lateFees = transactions.Where(p => p.Amount.HasValue && p.Description != null && p.Description.Contains("Late", StringComparison.OrdinalIgnoreCase)).Sum(p => Math.Abs(p.Amount.Value));
            var damageFees = transactions.Where(p => p.Amount.HasValue && p.Description != null && p.Description.Contains("Damage", StringComparison.OrdinalIgnoreCase)).Sum(p => Math.Abs(p.Amount.Value));
            
            
            decimal annualLeaseTotal = GetNoTaxTotals(transactions, annualLease);    
            decimal mhParkTotal = GetNoTaxTotals(transactions, mobileHome); 
           decimal storageTotal = transactions
                .Where(p => p != null
                        && p.TransType == "Payments Raised"
                        && !string.IsNullOrEmpty(p.Category)
                        && p.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase))
                .Sum(p => Math.Abs(p.Amount ?? 0m));


            var propaneTotal = transactions
            .Where(p => p != null
                && p.TransType == "Payments Raised"
                && !string.IsNullOrEmpty(p.Description)
                && p.Description.Contains("Propane", StringComparison.OrdinalIgnoreCase))
            .Sum(p => Math.Abs(p.Amount ?? 0));


            var mrgTotal = incomeRefundsSitesAndRentalsTotal;
            
            var transferFees = transactions
            .Where(p => p != null
                && p.TransType == "Payments Raised"
                && !string.IsNullOrEmpty(p.Description)
                && p.Description.Contains("Lease Transfer", StringComparison.OrdinalIgnoreCase))
            .Sum(p => Math.Abs(p.Amount ?? 0));


            var miscTotal = transactions
            .Where(p => p != null
                && p.TransType == "Payments Raised"
                && p.Description != null
                && (p.Description.Contains("Wash", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Trash", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Gas", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Bike", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Returned Check", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Credit Card Returned", StringComparison.OrdinalIgnoreCase)
                    || p.Description.Contains("Wristbands", StringComparison.OrdinalIgnoreCase)
                    || (p.Category != null && p.Category.Contains("Wheelchair", StringComparison.OrdinalIgnoreCase))))
            .Sum(p => Math.Abs(p.Amount ?? 0));
            
            var eventsTotal = transactions
            .Where(p => p != null
                && p.TransType == "Payments Raised"
                && !string.IsNullOrEmpty(p.Description)
                && p.Description.Contains("Oyster", StringComparison.OrdinalIgnoreCase))
            .Sum(p => Math.Abs(p.Amount ?? 0));

            var ccDeductions = transactions
            .Where(p => p != null
                && p.TransType == "Payments Raised"
                && !string.IsNullOrEmpty(p.PaymentMethod)
                && p.PaymentMethod.Contains("Authorize.Net", StringComparison.OrdinalIgnoreCase) &&
                p.Deposit != null &&
                p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase))
            .Sum(p => Math.Abs(p.Amount ?? 0));

            var cashDeductions = transactions
            .Where(p => p != null
                && p.TransType == "Payments Raised"
                && !string.IsNullOrEmpty(p.PaymentType)
                && p.PaymentType.Contains("cash", StringComparison.OrdinalIgnoreCase) &&
                p.Deposit != null &&
                p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase))
            .Sum(p => Math.Abs(p.Amount ?? 0));
            
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
                .Sum(p => Math.Abs(p.Amount ?? 0));

            var employeeTotal = employeePayments - employeeRefunds;
            
            decimal longTermSites = GetLongTermBookingTotal(startDate, siteCategories, transactions);
            decimal longTermRentals = GetLongTermBookingTotal(startDate, rentalCategories, transactions);

            // Calculate Manual_Refunds_NT
            decimal manualRefundsNT = 0.0m;

            // Combine all relevant categories
            string[] manualRefundCategories = annualLease.Concat(siteCategories)
                                                        .Concat(rentalCategories)
                                                        .Concat(mobileHome)
                                                        .Concat(employee)
                                                        .ToArray();

            // Filter transactions for manual refunds
            var manualRefundTransactions = transactions
                .Where(t =>
                    t.TransType != null &&
                    t.TransType.Contains("Refunds Raised", StringComparison.OrdinalIgnoreCase) &&
                    t.PaymentMethod != null &&
                    t.PaymentMethod.Contains("Manual", StringComparison.OrdinalIgnoreCase) &&
                    t.Amount.HasValue && 
                    t.Category != null &&
                    manualRefundCategories.Any(c => t.Category.Contains(c, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Check each transaction against the database for checked-in status
            foreach (var refund in manualRefundTransactions)
            {
                if (refund.AccountForId != null && refund.TransDate.HasValue)
                {
                    bool hasCheckedInBefore = HasCheckedInBeforeDate(refund.AccountForId, refund.TransDate.Value);

                    if (hasCheckedInBefore)
                    {
                        manualRefundsNT += Math.Abs(refund.Amount.Value);
                    }
                }
            }

            decimal longTermSitesRefundsNT = 0.0m;
            decimal longTermRentalsRefundsNT = 0.0m;

            // Filter transactions for long-term sites and rentals
            var longTermTransactions = transactions
                .Where(t =>
                    t.TransType != null &&
                    t.TransType.Contains("Refunds Raised", StringComparison.OrdinalIgnoreCase) &&
                    t.PaymentMethod != null &&
                    t.PaymentMethod.Contains("Manual", StringComparison.OrdinalIgnoreCase) &&
                    t.Amount.HasValue && t.Amount > 0 &&
                    t.Category != null &&
                    (siteCategories.Any(c => t.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                    rentalCategories.Any(c => t.Category.Contains(c, StringComparison.OrdinalIgnoreCase))) &&
                    t.ArrivalDate.HasValue && t.DepartureDate.HasValue &&
                    (t.DepartureDate.Value - t.ArrivalDate.Value).TotalDays >= 90)
                .ToList();

            // Check each transaction against the database for checked-in status
            foreach (var refund in longTermTransactions)
            {
                if (refund.AccountForId != null && refund.TransDate.HasValue)
                {
                    bool hasCheckedInBefore = HasCheckedInBeforeDate(refund.AccountForId, refund.TransDate.Value);

                    if (hasCheckedInBefore)
                    {
                        if (siteCategories.Any(c => refund.Category.Contains(c, StringComparison.OrdinalIgnoreCase)))
                        {
                            longTermSitesRefundsNT += Math.Abs(refund.Amount.Value);
                        }
                        else if (rentalCategories.Any(c => refund.Category.Contains(c, StringComparison.OrdinalIgnoreCase)))
                        {
                            longTermRentalsRefundsNT += Math.Abs(refund.Amount.Value);
                        }
                    }
                }
            }

            decimal lockFeesTotal = CalculateLockFees(transactions, startDate);

             if (hasMatchingDate)
            {
                deposits.Sites = sitesTotal;
                deposits.Rentals = rentalsTotal;
                deposits.Lock_Fees = lockFeesTotal;
                deposits.Extra_Vehicle_Fees = extraVehiclesTotal;
                deposits.Damage_Fees = damageFees;
                deposits.Late_Fees = lateFees;
                deposits.MRG1 = mrgTotal;
                deposits.Visitor_Fees = extraVisitorsTotal + extraVisitorsTotalWithAccom;
                deposits.Annual_Leases = annualLeaseTotal;
                deposits.Employee = employeeTotal;
                deposits.LT_Sites = longTermSites;
                deposits.LT_Rentals = longTermRentals;
                deposits.MH_Park = mhParkTotal;
                deposits.MRG2 = manualRefundsNT + longTermSitesRefundsNT + longTermRentalsRefundsNT;
                deposits.Storage = storageTotal;
                deposits.Propane = propaneTotal;
                deposits.Events = eventsTotal;
                deposits.Misc = miscTotal;
                deposits.Transfer_Fees = transferFees;
            }}};

             private async Task<Decimal> GetTotals(string[] categories, decimal total, List<Dictionary<string, object>> checkedInList)
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

                    if (record.TryGetValue("DepositsHeld", out var depVal) && depVal != null && depVal != DBNull.Value)
                        decimal.TryParse(Convert.ToString(depVal), out depositHeld);

                    if (record.TryGetValue("PaymentsAfterCheckIn", out var afterVal) && afterVal != null && afterVal != DBNull.Value)
                        decimal.TryParse(Convert.ToString(afterVal), out paymentsAfter);

                    if (depositHeld != 0 || paymentsAfter != 0)
                    {
                        decimal totalForSite = depositHeld + paymentsAfter;
                        total += Math.Abs(totalForSite);
                    }
                }
            }
            return total;
        }


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
                    t.AccountForId != null &&
                    t.TransDate != null 
                )
                .ToList();

                foreach (var t in annualLeaseTransactions)
                {
                    // Check database to see if this booking has checked in before the transaction date
                    bool hasCheckedInBefore = HasCheckedInBeforeDate(t.AccountForId, t.TransDate.Value);

                    if (hasCheckedInBefore)
                    {
                        total += Math.Abs(t.Amount ?? 0m);
                    }
                }

                return total;
            }


       private decimal GetLongTermBookingTotal(DateTime transDate, string[] categories, List<TransactionFlow> transactions)
        {
            decimal sqlTotal = 0m;
            decimal transactionTotal = 0m;

            // ------------------------------------------
            // 1. SQL QUERY TOTAL
            // ------------------------------------------

            // Build dynamic LIKE clauses
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

            // ------------------------------------------
            // 2. IN-MEMORY TRANSACTION TOTAL
            // ------------------------------------------

            // I assume TransactionFlow contains:
            // - int AccountForId (booking ID)
            // - decimal? Amount
            // - DateTime Arrival
            // - DateTime Departure
            // - string Site

            foreach (var t in transactions)
            {
                bool isLongTerm = false;

                if (t.ArrivalDate.HasValue && t.DepartureDate.HasValue)
                {
                    isLongTerm = (t.DepartureDate.Value - t.ArrivalDate.Value).TotalDays >= 90;
                }


                
                bool categoryMatch =
                // Category must match
                categories.Any(cat =>
                    !string.IsNullOrEmpty(t.Category) &&
                    t.Category.Contains(cat, StringComparison.OrdinalIgnoreCase)
                )
                &&
                // AND description must contain "accommodation" or "accomodation"
                !string.IsNullOrEmpty(t.Description) &&
                (
                    t.Description.Contains("accommodation", StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains("accomodation", StringComparison.OrdinalIgnoreCase)
                );


                bool checkedInBefore = HasCheckedInBeforeDate(t.AccountForId, transDate);

                if (isLongTerm && categoryMatch && checkedInBefore)
                    transactionTotal += Math.Abs(t.Amount ?? 0);
            }

            // ------------------------------------------
            // 3. RETURN SQL + In-Memory totals
            // ------------------------------------------
            return sqlTotal + transactionTotal;
        }


        
private decimal CalculateLockFees(List<TransactionFlow> list, DateTime reportDate)
{
    decimal lockFee = reportDate < new DateTime(2024, 1, 1) ? 30m : 40m;
    decimal siteDeposit = 100m;
    decimal rentalDeposit = 150m;

    // Local helper to match legacy "skip transfers"
    bool IsTransfer(TransactionFlow t) =>
        (t.Description ?? "").Contains("Transfer", StringComparison.OrdinalIgnoreCase) ||
        (t.Category ?? "").Contains("Transfer", StringComparison.OrdinalIgnoreCase) ||
        (t.TransType ?? "").Contains("Transfer", StringComparison.OrdinalIgnoreCase) ||
        (t.TranslatedPaymentType ?? "").Contains("Transfer", StringComparison.OrdinalIgnoreCase);

    // Build a list then sum (as you requested)
    var lockFeeList =
        list.Where(t => t.Amount.HasValue)
            .Select(t => new
            {
                Amt = Math.Abs(t.Amount.Value),
                Desc = t.Description ?? "",
                Cat = t.Category ?? "",
                Type = t.TransType ?? "",
                PayType = t.TranslatedPaymentType ?? "",
                IsTransfer = IsTransfer(t)
            })
            // Must be Payments Raised
            .Where(t => t.Type.Contains("Payments Raised", StringComparison.OrdinalIgnoreCase))
            // Evaluate according to legacy rules
            .Select(t =>
            {
                decimal fee = 0m;

                // 1. Combined deposit + lock fee (140 / 190)
                if (!t.IsTransfer &&
                    (t.Amt == siteDeposit + lockFee || t.Amt == rentalDeposit + lockFee))
                {
                    fee = lockFee;
                }
                // 2. Standalone lock fee (40)
                else if (!t.IsTransfer &&
                         t.Amt == lockFee &&
                        (t.Desc.Contains("Booking Modification", StringComparison.OrdinalIgnoreCase) ||
                         t.Desc.Contains("RESTORED CREDIT CARD", StringComparison.OrdinalIgnoreCase)))
                {
                    fee = lockFee;
                }

                return fee;
            })
            // Keep only positive lock fees
            .Where(f => f > 0)
            .ToList();

    // Finally sum the list
    return lockFeeList.Sum();
}
