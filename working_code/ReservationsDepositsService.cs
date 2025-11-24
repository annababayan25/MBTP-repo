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

// ReservationsDepositsService is a class dedicated to Reservations Deposits Table (Daily Breakdown R)
namespace MBTP.Services
{
    public class ReservationsDepositsService : NewbookBaseApi
    {
        private readonly TransactionFlowApi _transactionFlowApi;
        private readonly DailyReport _dailyReport;
        private readonly IDatabaseConnectionService _dbConnectionService;

        public ReservationsDepositsService(IDatabaseConnectionService dbConnectionService, HttpClient client, DailyReport dailyReport, TransactionFlowApi transactionFlowApi)
            : base(client)
        {
            _dbConnectionService = dbConnectionService;
            _transactionFlowApi = transactionFlowApi;
            _dailyReport = dailyReport;
        }

        public async Task<List<ReservationsDeposits>> ProcessReservationsDepositsAsync(DateTime startDate, DateTime endDate)
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
            
            //END: Retrieve the Checked In List DataSet and convert it to a list

            var depositsList = new List<ReservationsDeposits>();
            var deposits = new ReservationsDeposits
            {
                TransDate = startDate,
            };

            string[] siteCategories = { "WESC", "Water & Electric Only" };
            string[] rentalCategories = { "Ocean Villa", "Cottage", "Cabin", "Travel Trailer" };
            string[] extrasVehicles = { "Extra Vehicle", "Vehicle", "Vehicles", "Extra Vehicles Fee", "Extra Vehicle Fees", "Extra" };
            string[] annualLease = {"Annual", "Annual Lease", "ANNUAL LEASE", "A/L"};
            string[] employee = {"Employee"};
            string[] mobileHome = {"Mobile", "Mobile Home", "M/L", "ML"};
            string[] storage = {"Storage"};
            string[] security = {"Security"};

            //START: Retrieve all deposits taken for the day for SITES and RENTALS
            var depositsHeldList = transactions
            .Where(p =>
                p.Category != null &&
                (
                    siteCategories.Any(c => p.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                    rentalCategories.Any(c => p.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
                )
                &&
                !p.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
                p.Deposit != null &&
                p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase) &&
                (p.Description == null || !p.Description.Contains("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase))
            )
            .OrderBy(p => p.AccountForId)
            .ToList();

            // Exclude deposits that have any refund for the same PaymentTypeReference + AccountForId on the same day
            var depositsHeldListNoRefunds = depositsHeldList.Where(p =>
            {
                if (string.IsNullOrEmpty(p.PaymentTypeReference) || p.AccountForId == null)
                    return true;

                bool hasRefund = transactions.Any(r =>
                    r.AccountForId == p.AccountForId &&
                    r.PaymentTypeReference == p.PaymentTypeReference &&
                    r.TransType == "Refunds Raised" &&
                    (r.Amount ?? 0) > 0);

                return !hasRefund;
            }).ToList();

            // Only keep deposits that were taken before check-in.
            // Remove deposits where the guest already checked in before the payment was taken.
            var depositsHeldListFiltered = depositsHeldListNoRefunds.Where(t =>
            {
                if (t.AccountForId == null || t.TransDate == null)
                    return false;

                if (!DateTime.TryParse(Convert.ToString(t.TransDate), out DateTime transDate))
                    return false;

                var match = checkedInList.FirstOrDefault(c =>
                    c.TryGetValue("BookingId", out var bookingIdVal) &&
                    bookingIdVal != DBNull.Value &&
                    Convert.ToString(bookingIdVal) == Convert.ToString(t.AccountForId)
                );

                DateTime? checkedInDate = null;
                if (match != null &&
                    match.TryGetValue("BookingCheckedIn", out var checkedInVal) &&
                    checkedInVal != DBNull.Value &&
                    DateTime.TryParse(Convert.ToString(checkedInVal), out DateTime parsedCheckedIn))
                {
                    checkedInDate = parsedCheckedIn;
                }

                if (t.ArrivalDate.HasValue && t.ArrivalDate.Value.Date > transDate.Date)
                    return true;

                if (t.ArrivalDate.HasValue && t.ArrivalDate.Value.Date == transDate.Date)
                {
                    if (checkedInDate.HasValue)
                        return checkedInDate.Value > transDate;
                    return true;
                }

                if (checkedInDate.HasValue && checkedInDate.Value < transDate)
                    return false;

                return true;
            }).ToList();

            // START: Golf Carts
            var golfCartDepositsHeldList = transactions.Where(c => c.Category != null && c.Category.Contains("Golf Cart", StringComparison.OrdinalIgnoreCase)).ToList();
            decimal golfCartDepositsAppliedTotal = 0;

            if (checkedInList != null && checkedInList.Count > 0)
            {
                foreach (var record in checkedInList)
                {
                    if (record.TryGetValue("Site", out var siteVal) &&
                        siteVal != null &&
                        siteVal != DBNull.Value)
                    {
                        string siteStr = Convert.ToString(siteVal);

                        if (!string.IsNullOrWhiteSpace(siteStr) &&
                            siteStr.Contains("GOLF CART", StringComparison.OrdinalIgnoreCase))
                        {
                            if (record.TryGetValue("DepositsHeld", out var depVal) &&
                                depVal != null &&
                                depVal != DBNull.Value &&
                                decimal.TryParse(Convert.ToString(depVal), out decimal depositHeld))
                            {
                                golfCartDepositsAppliedTotal += Math.Abs(depositHeld);
                            }
                        }
                    }
                }
            }

            //START: Fetch Deposits Applied for Sites
            decimal siteDepositsAppliedTotal = 0.0m;
            siteDepositsAppliedTotal = await GetDepositsApplied(siteCategories, siteDepositsAppliedTotal, checkedInList);
            //END: Fetch Deposits Applied for Sites

            //START: Fetch Deposits Applied for Rentals
            decimal rentalDepositsAppliedTotal = 0.0m;
            rentalDepositsAppliedTotal = await GetDepositsApplied(rentalCategories, rentalDepositsAppliedTotal, checkedInList);
            //END: Fetch Deposits Applied for Rentals

            // Split deposits held into Arrived / Confirmed
            var arrivedList = depositsHeldListFiltered.Where(t =>
            {
                if (!t.ArrivalDate.HasValue || t.TransDate == null)
                    return false;

                if (!DateTime.TryParse(Convert.ToString(t.TransDate), out DateTime transDate))
                    return false;

                var match = checkedInList.FirstOrDefault(c =>
                    c.TryGetValue("BookingId", out var bookingIdVal) &&
                    bookingIdVal != DBNull.Value &&
                    Convert.ToString(bookingIdVal) == Convert.ToString(t.AccountForId)
                );

                if (match == null)
                    return false;

                DateTime? checkedInDate = null;
                if (match.TryGetValue("BookingCheckedIn", out var checkedInVal) &&
                    checkedInVal != DBNull.Value &&
                    DateTime.TryParse(Convert.ToString(checkedInVal), out DateTime parsedCheckedIn))
                {
                    checkedInDate = parsedCheckedIn;
                }

                if (checkedInDate.HasValue && checkedInDate.Value.Date == transDate.Date && checkedInDate.Value > transDate)
                    return true;

                return false;
            }).ToList();

            var confirmedList = depositsHeldListFiltered
                .Where(t => !arrivedList.Any(a => a.ItemId == t.ItemId))
                .ToList();

            var arrivedSitesList = FilterTransactions(arrivedList, siteCategories).ToList();
            var arrivedRentalsList = FilterTransactions(arrivedList, rentalCategories).ToList();
            var confirmedSitesList = FilterTransactions(confirmedList, siteCategories).ToList();
            var confirmedRentalsList = FilterTransactions(confirmedList, rentalCategories).ToList();

            // Filter MANUAL refund transactions first
            var refundTransactions = transactions
                .Where(t =>
                    t.TransType != null &&
                    t.TranslatedPaymentType != null &&
                    t.TransType.Contains("Refunds Raised", StringComparison.OrdinalIgnoreCase) &&
                    !t.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase) &&
                    t.PaymentMethod != null && t.PaymentMethod.Contains("Manual", StringComparison.OrdinalIgnoreCase) &&
                    t.Amount.HasValue && t.Amount > 0 &&
                    t.ArrivalDate.HasValue &&
                    t.DepartureDate.HasValue &&
                    t.TransDate != null &&
                    DateTime.TryParse(Convert.ToString(t.TransDate), out _)
                )
                .ToList();

            var depositRefunds = new List<TransactionFlow>();
            var incomeRefunds = new List<TransactionFlow>();

            foreach (var refundTransaction in refundTransactions)
            {
                if (refundTransaction.ArrivalDate == null ||
                    refundTransaction.DepartureDate == null ||
                    refundTransaction.TransDate == null)
                {
                    Console.WriteLine($"Skipping {refundTransaction.AccountForId}: missing dates.");
                    continue;
                }

                DateTime transDate = refundTransaction.TransDate.Value;
                DateTime arrivalDate = refundTransaction.ArrivalDate.Value;
                DateTime departureDate = refundTransaction.DepartureDate.Value;

                // Get checked-in date, if any
                DateTime? checkedInDate = null;
                var match = checkedInList.FirstOrDefault(c =>
                    c.TryGetValue("BookingId", out var bookingIdVal) &&
                    bookingIdVal != DBNull.Value &&
                    Convert.ToString(bookingIdVal) == Convert.ToString(refundTransaction.AccountForId));

                if (match != null &&
                    match.TryGetValue("BookingCheckedIn", out var checkedInVal) &&
                    checkedInVal != DBNull.Value &&
                    DateTime.TryParse(Convert.ToString(checkedInVal), out DateTime parsedCheckedIn))
                {
                    checkedInDate = parsedCheckedIn;
                }

                bool depositRefund = false;
                bool incomeRefund = false;

                if (transDate < arrivalDate)
                {
                    // Refund before arrival -> always deposit
                    depositRefund = true;
                    Console.WriteLine($"Booking {refundTransaction.AccountForId}: Refund BEFORE arrival -> Deposit refund");
                }

                else if (transDate >= arrivalDate)
                {
                    bool checkedInBeforeRefund = HasCheckedInBeforeDate(refundTransaction.AccountForId, transDate);

                    if (checkedInBeforeRefund)
                    {
                        // Refund after arrival, guest did check in then income refund
                        incomeRefund = true;
                        Console.WriteLine($"Booking {refundTransaction.AccountForId}: Refund AFTER arrival, guest checked in -> Income refund");
                        incomeRefunds.Add(refundTransaction);
                    }
                    else
                    {
                        // Refund after arrival, guest NOT checked in then deposit refund
                        depositRefund = true;
                        Console.WriteLine($"Booking {refundTransaction.AccountForId}: Refund AFTER arrival, guest NOT checked in -> Deposit refund");
                    }
                }

                // Optional long-term stay check 
                if ((departureDate - arrivalDate).TotalDays >= 90)
                {
                    bool checkedInBeforeRefund = HasCheckedInBeforeDate(refundTransaction.AccountForId, transDate);
                    if (checkedInBeforeRefund)
                    {
                        depositRefund = false;
                        incomeRefund = true;
                        Console.WriteLine($"Booking {refundTransaction.AccountForId}: Long-term stay (90+ days) -> Income refund");
                    }
                }

                if (depositRefund && !incomeRefund) depositRefunds.Add(refundTransaction);
                if (incomeRefund && !depositRefund) incomeRefunds.Add(refundTransaction);
            }

            refundTransactions = depositRefunds;

            var noVoidedrefunds = refundTransactions.Where(p =>
            {
                if (string.IsNullOrEmpty(p.PaymentTypeReference) || p.AccountForId == null)
                    return true;

                bool hasRefund = transactions.Any(r =>
                    r.AccountForId == p.AccountForId &&
                    r.ItemId == p.ItemId &&
                    r.TransType == "Voided Refunds Voided");
                return !hasRefund;
            }).ToList();

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

            var refundSitesList = noVoidedrefunds
                .Where(r => r.Category != null && siteCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) && !r.Category.Contains("Storage")
                && r.TranslatedPaymentType != null && !r.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var refundRentalsList = noVoidedrefunds
                .Where(r => r.Category != null && rentalCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) && !r.Category.Contains("Storage")
                && r.TranslatedPaymentType != null && !r.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var refundGolfCarts = noVoidedrefunds.Where(r => r.Category != null && r.Category.Contains("Golf Cart", StringComparison.OrdinalIgnoreCase) && !r.Category.Contains("Storage")
                && r.TranslatedPaymentType != null && !r.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var refundSitesTotal = refundSitesList.Sum(r => Math.Abs(r.Amount ?? 0));
            var refundRentalsTotal = refundRentalsList.Sum(r => Math.Abs(r.Amount ?? 0));

            // START: Gift Vouchers
            var giftVouchersPurchases = GiftVouchers(transactions);
            var giftVouchersSites = GiftVouchers(transactions, categoryFilters: siteCategories);
            var giftVouchersRentals = GiftVouchers(transactions, categoryFilters: rentalCategories);
            var giftVouchersStorage = GiftVouchers(transactions,singleCategory: "Storage");
            // E: Gift Vouchers

            bool hasMatchingDate = transactions.Any(p =>
            {
                if (p.TransDate == null) return false;
                var dateValue = Convert.ToDateTime(p.TransDate);
                return dateValue.Date == startDate.Date;
            });

            var orderedConfirmedSitesList = confirmedSitesList.OrderBy(ite => ite.AccountForId).ToList();
            decimal totalAmount = 0;

            foreach (var ite in orderedConfirmedSitesList)
            {
                Console.WriteLine($"confirmedSitesList: BookingId: {ite.AccountForId}, Amount: {ite.Amount}, TransType: {ite.TransType}, PaymentMethod: {ite.PaymentMethod}");
                totalAmount += ite.Amount ?? 0; // Assuming Amount is nullable
            }

            Console.WriteLine($"Total Amount: {totalAmount}");

            // Update the ProcessReservationsDepositsAsync method to include the new logic
            var (siteSecurityDeposits, rentalSecurityDeposits) = await GetSecurityDepositsForDay(startDate, siteCategories, rentalCategories); 

            if(hasMatchingDate)
            {
                deposits.SiteDepTaken = confirmedSitesList.Sum(p => Math.Abs(p.Amount ?? 0)) + siteSecurityDeposits;
                deposits.SiteDepApp = siteDepositsAppliedTotal;
                deposits.SiteDepMRG = refundSitesList.Sum(x => x.Amount ?? 0);
                deposits.RentalDepTaken = confirmedRentalsList.Sum(p => Math.Abs(p.Amount ?? 0)) + rentalSecurityDeposits;
                deposits.RentalDepApp = rentalDepositsAppliedTotal;
                deposits.RentalDepMRG = refundRentalsList.Sum(x => x.Amount ?? 0);
                deposits.GolfDepTaken = golfCartDepositsHeldList.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.GolfDepApp = golfCartDepositsAppliedTotal;
                deposits.GolfDepMRG = refundGolfCarts.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.VouchersPurch = giftVouchersPurchases.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.VouchersRedSite = giftVouchersSites.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.VouchersRedRental = giftVouchersRentals.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.VouchersRedStorage = giftVouchersStorage.Sum(p => Math.Abs(p.Amount ?? 0));

                depositsList.Add(deposits);
            }
            return depositsList;
        }
    

        private bool MatchesCategory(string category, params string[] keywords) =>
            keywords.Any(keyword => category.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        private bool MatchesDeposit(string deposit) =>
            deposit != null && deposit.Contains("1", StringComparison.OrdinalIgnoreCase);

        private bool MatchesDescription(string description) =>
            description == null || !description.Contains("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase);

        private IEnumerable<TransactionFlow> FilterTransactions(IEnumerable<TransactionFlow> transactions, string[] categories)
        {
            return transactions.Where(p =>
                p.Category != null &&
                MatchesCategory(p.Category, categories) &&
                MatchesDeposit(p.Deposit) &&
                MatchesDescription(p.Description) &&
                (p.TranslatedPaymentType == null || !p.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase)));
        }

        private bool HasCheckedInBeforeDate(int bookingId, DateTime date)
        {
            const string sqlQuery = @"
                SELECT COUNT(1)
                FROM dbo.CheckedIn
                WHERE BookingId = @BookingId
                AND BookingCheckedIn <= @date";

            using (var sqlConn = _dbConnectionService.CreateConnection())
            using (var cmd = new SqlCommand(sqlQuery, sqlConn))
            {
                cmd.Parameters.Add("@BookingId", SqlDbType.Int).Value = bookingId;
                cmd.Parameters.Add("@date", SqlDbType.DateTime2).Value = date;

                sqlConn.Open();
                int count = (int)cmd.ExecuteScalar();
                return count > 0;
            }
        }

        private async Task<(decimal siteSecurityDeposits, decimal rentalSecurityDeposits)> GetSecurityDepositsForDay(DateTime date, string[] siteCategories, string[] rentalCategories)
        {
            const string sqlQuery = @"
                SELECT Site, SecurityDeposits, BookingName
                FROM dbo.CheckedIn
                WHERE CAST(BookingCheckedIn AS DATE) = @date";

            decimal siteSecurityDeposits = 0m;
            decimal rentalSecurityDeposits = 0m;

            using (var sqlConn = _dbConnectionService.CreateConnection())
            using (var cmd = new SqlCommand(sqlQuery, sqlConn))
            {
                cmd.Parameters.Add("@date", SqlDbType.Date).Value = date;

                sqlConn.Open();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        string bookingName = reader["BookingName"]?.ToString() ?? string.Empty;

                        // Skip rows where BookingName contains "BLOCKED"
                        if (bookingName.Contains("BLOCKED", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string site = reader["Site"]?.ToString() ?? string.Empty;
                        decimal securityDeposit = reader["SecurityDeposits"] != DBNull.Value
                            ? Convert.ToDecimal(reader["SecurityDeposits"])
                            : 0m;

                        if (siteCategories.Any(c => site.Contains(c, StringComparison.OrdinalIgnoreCase)))
                        {
                            siteSecurityDeposits += securityDeposit;
                        }
                        else if (rentalCategories.Any(c => site.Contains(c, StringComparison.OrdinalIgnoreCase)))
                        {
                            rentalSecurityDeposits += securityDeposit;
                        }
                    }
                }
            }

            return (siteSecurityDeposits, rentalSecurityDeposits);
        }



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

        private async Task<Decimal> GetDepositsApplied(string[] categories, decimal total, List<Dictionary<string, object>> checkedInList)
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

        private List<TransactionFlow> GiftVouchers(IEnumerable<TransactionFlow> transactions,IEnumerable<string>? categoryFilters = null,string? singleCategory = null)
        {
            return transactions
                .Where(r =>
                    r.Description != null &&
                    r.Description.Contains("Gift", StringComparison.OrdinalIgnoreCase) &&
                    r.PaymentTypeAction?.Contains("Payments") == true &&
                    (r.Amount ?? 0) < 0 &&
                    (
                        categoryFilters == null && singleCategory == null
                        ? r.Category == null
                        : (
                            (categoryFilters != null && categoryFilters.Any(c =>
                                r.Category?.Contains(c, StringComparison.OrdinalIgnoreCase) == true))
                            ||
                            (singleCategory != null &&
                                r.Category?.Contains(singleCategory, StringComparison.OrdinalIgnoreCase) == true)
                        )
                    )
                )
                .ToList();
        }

    }
}
            
        
        

