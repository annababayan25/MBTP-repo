using System.Data;
using MBTP.Models;
using Newtonsoft.Json;
using MBTP.Retrieval;
using System.Linq;
using System.Reflection.Metadata;


namespace MBTP.Services
{
    public class DepositsHeldService : NewbookBaseApi
    {
        private readonly TransactionFlowApi _transactionFlowApi;
        private readonly DailyReport _dailyReport;
        private readonly ChargesApi _chargesApi;

        public DepositsHeldService(HttpClient client, ChargesApi chargesApi, DailyReport dailyReport, TransactionFlowApi transactionFlowApi) 
            : base(client)
        {
            _transactionFlowApi = transactionFlowApi;
            _dailyReport = dailyReport;
            // _chargesApi = chargesApi;
        }

        public async Task<List<ReservationsDeposits>> ProcessDepositsHeldAsync(DateTime startDate, DateTime endDate)
        {
            var transactions = await _transactionFlowApi.PopulateTransactions(startDate, endDate);
            // var charges = await _chargesApi.PopulateCharges(startDate, endDate);
            DataSet ds = await _dailyReport.RetrieveCheckInsReport(startDate, endDate);

            List<Dictionary<string, object>> checkedInList = new();

            if (ds != null && ds.Tables.Count > 0)
            {
                DataTable table = ds.Tables[0];
                if (table.Rows.Count > 0)
                {
                    checkedInList = table.AsEnumerable()
                        .Select(row => table.Columns
                            .Cast<DataColumn>()
                            .ToDictionary(col => col.ColumnName, col => row[col]))
                        .ToList();
                }
            }
            var depositsList = new List<ReservationsDeposits>();

            var deposits = new ReservationsDeposits
            {
                DepositDate = startDate,
                Sites_Deposits_Taken = 0.0m,
                Sites_Deposits_Applied = 0.0m,
                Sites_Manual_Refunds = 0.0m,
                Rentals_Deposits_Taken = 0.0m,
                Rentals_Deposits_Applied = 0.0m,
                Rentals_Manual_Refunds = 0.0m,
                Golf_Cart_Deposits_Taken = 0.0m,
                Golf_Cart_Deposits_Applied = 0.0m,
                Golf_Cart_Manual_Refunds = 0.0m,
                Gift_Vouchers_Purchased = 0.0m,
                Gift_Vouchers_Redeemed_For_Sites = 0.0m,
                Gift_Vouchers_Redeemed_For_Rentals = 0.0m,
                Gift_Vouchers_Redeemed_For_Storage = 0.0m,
            };

            var depositsHeldList = transactions
                .Where(p =>
                    p.Category != null &&
                    (
                        p.Category.Contains("WESC", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Security Deposit", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Ocean Villa", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Cottage", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Cabin", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Travel Trailer", StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains("Employee Site", StringComparison.OrdinalIgnoreCase) || 
                        p.Description.Contains("Security Deposit", StringComparison.OrdinalIgnoreCase)
                    ) &&
                    !p.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&

                    p.Deposit != null &&
                    p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase) &&
                    (p.Description == null || !p.Description.Contains("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase))
                ).ToList().OrderBy(p => p.AccountForId);

            // Exclude deposits that have any refund for the same PaymentTypeReference + AccountForId
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

            var golfCartDepositsHeldList = transactions.Where(c => c.Category != null && c.Category.Contains("Golf Cart", StringComparison.OrdinalIgnoreCase)).ToList();

            decimal golfCartDepositsAppliedTotal = 0;
            List<Dictionary<string, object>> golfCartAppliedList = new();

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
                            golfCartAppliedList.Add(record);
                        }
                    }
                }
            }
        }

        string[] siteCategories = { "WESC", "Employee Site", "Security Deposit" };
        string[] rentalCategories = { "Ocean Villa", "Cottage", "Cabin", "Travel Trailer", "Security Deposit" };
            
        decimal siteDepositsAppliedTotal = 0m;
        List<Dictionary<string, object>> sitesDepositsAppliedList = new();

        if (checkedInList != null && checkedInList.Count > 0)
        {

            foreach (var record in checkedInList)
            {
                if (!record.TryGetValue("Site", out var siteVal) || siteVal == null || siteVal == DBNull.Value)
                    continue;

                string siteStr = Convert.ToString(siteVal);
                if (string.IsNullOrWhiteSpace(siteStr))
                    continue;

                bool isSiteCategory = siteCategories.Any(c => siteStr.Contains(c, StringComparison.OrdinalIgnoreCase))
                && !siteStr.Contains("Storage", StringComparison.OrdinalIgnoreCase);
                if (!isSiteCategory)
                    continue;

                if (record.TryGetValue("DepositsHeld", out var depVal) &&
                    depVal != null &&
                    depVal != DBNull.Value &&
                    decimal.TryParse(Convert.ToString(depVal), out decimal depositHeld))
                {
                    siteDepositsAppliedTotal += Math.Abs(depositHeld);
                    sitesDepositsAppliedList.Add(record);
                }
            }
        }

        decimal rentalDepositsAppliedTotal = 0m;
        List<Dictionary<string, object>> rentalDepositsAppliedList = new();

        if (checkedInList != null && checkedInList.Count > 0)
        {

            foreach (var record in checkedInList)
            {
                if (!record.TryGetValue("Site", out var rentalVal) || rentalVal == null || rentalVal == DBNull.Value)
                    continue;

                string rentalStr = Convert.ToString(rentalVal);
                if (string.IsNullOrWhiteSpace(rentalStr))
                    continue;

                bool isRentalCategory = rentalCategories.Any(c => rentalStr.Contains(c, StringComparison.OrdinalIgnoreCase)) 
                && !rentalStr.Contains("Storage", StringComparison.OrdinalIgnoreCase);
                if (!isRentalCategory)
                    continue;

                if (record.TryGetValue("DepositsHeld", out var depVal) &&
                    depVal != null &&
                    depVal != DBNull.Value &&
                    decimal.TryParse(Convert.ToString(depVal), out decimal depositHeld))
                {
                    rentalDepositsAppliedTotal += Math.Abs(depositHeld);
                    rentalDepositsAppliedList.Add(record);
                }
            }
        }
        
            //var securityDepositsHeldList = charges
            //   .Where(x => x.Description.Contains("Security", StringComparison.OrdinalIgnoreCase))
            //    .ToList();

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

            var refundTransactions = transactions
                .Where(t => t.TransType != null && t.TranslatedPaymentType != null &&
                            t.TransType.Equals("Refunds Raised", StringComparison.OrdinalIgnoreCase) &&
                            !t.TranslatedPaymentType.Equals("Balance Transfer", StringComparison.OrdinalIgnoreCase) &&
                            t.Amount.HasValue && t.Amount > 0)
                .ToList();

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

            var refundSitesList = noVoidedrefunds
                .Where(r => r.Category != null && siteCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) && !r.Category.Contains("Storage")
                && r.TranslatedPaymentType != null && !r.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var refundRentalsList = noVoidedrefunds
                .Where(r => r.Category != null && rentalCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) && !r.Category.Contains("Storage")
                && r.TranslatedPaymentType != null && !r.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var refundSitesManualEntry = refundSitesList
                .Where(r => r.PaymentMethod != null && r.PaymentMethod.Contains("Manual", StringComparison.OrdinalIgnoreCase) && r.Category != null && !r.Category.Contains("Storage")
                && r.TranslatedPaymentType != null && !r.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var refundRentalsManualEntry = refundRentalsList
                .Where(r => r.PaymentMethod != null && r.PaymentMethod.Contains("Manual", StringComparison.OrdinalIgnoreCase) && r.Category != null && !r.Category.Contains("Storage")
                && r.TranslatedPaymentType != null && !r.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var refundGolfCarts = noVoidedrefunds.Where(r => r.Category != null && r.Category.Contains("Golf Cart", StringComparison.OrdinalIgnoreCase) && !r.Category.Contains("Storage")
            && r.TranslatedPaymentType != null && !r.TranslatedPaymentType.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var refundSitesTotal = refundSitesList.Sum(r => Math.Abs(r.Amount ?? 0));
            var refundRentalsTotal = refundRentalsList.Sum(r => Math.Abs(r.Amount ?? 0));

            bool hasMatchingDate = transactions.Any(p =>
            {
                if (p.TransDate == null) return false;
                var dateValue = Convert.ToDateTime(p.TransDate);
                return dateValue.Date == startDate.Date;
            });

            var giftVouchersPurchases = transactions
                .Where(r => r.Category == null &&
                r.Description != null &&
                r.Description.Contains("Gift", StringComparison.OrdinalIgnoreCase) &&
                r.PaymentTypeAction.Contains("Payments") &&
                (r.Amount ?? 0) < 0).ToList();

            var giftVouchersSites = transactions
            .Where(r => siteCategories.Any(c => r.Category?.Contains(c, StringComparison.OrdinalIgnoreCase) == true) &&
                        r.Description != null &&
                        r.Description.Contains("Gift") &&
                        r.PaymentTypeAction?.Contains("Payments") == true &&
                        (r.Amount ?? 0) < 0).ToList();

            var giftVouchersRentals = transactions
            .Where(r => rentalCategories.Any(c => r.Category?.Contains(c, StringComparison.OrdinalIgnoreCase) == true) &&
                        r.Description != null &&
                        r.Description.Contains("Gift", StringComparison.OrdinalIgnoreCase) &&
                        r.PaymentTypeAction?.Contains("Payments") == true &&
                        (r.Amount ?? 0) < 0).ToList();

            var giftVouchersStorage = transactions
            .Where(r => (r.Category?.Contains("Storage", StringComparison.OrdinalIgnoreCase) == true) &&
                        r.Description != null &&
                        r.Description.Contains("Gift", StringComparison.OrdinalIgnoreCase) &&
                        r.PaymentTypeAction?.Contains("Payments") == true &&
                        (r.Amount ?? 0) < 0).ToList();

            // --- Handle Security Deposits based on check-in status ---
            /*
            decimal sitesSecurityDepositsApplied = 0m;
            decimal rentalsSecurityDepositsApplied = 0m;
            decimal sitesSecurityDepositsTaken = 0m;
            decimal rentalsSecurityDepositsTaken = 0m;

            foreach (var secDep in securityDepositsHeldList)
            {
                if (secDep.AccountForId == null || secDep.Amount == null)
                    continue;

                // Try to find matching booking in checkedInList
                var match = checkedInList.FirstOrDefault(c =>
                    c.TryGetValue("BookingId", out var bookingIdVal) &&
                    bookingIdVal != DBNull.Value &&
                    Convert.ToString(bookingIdVal) == Convert.ToString(secDep.AccountForId));

                bool isCheckedIn = false;
                if (match != null &&
                    match.TryGetValue("BookingCheckedIn", out var checkedInVal) &&
                    checkedInVal != DBNull.Value &&
                    DateTime.TryParse(Convert.ToString(checkedInVal), out _))
                {
                    isCheckedIn = true;
                }

                // Determine if it’s a site or rental deposit
                bool isSite = secDep.Description != null &&
                            siteCategories.Any(c => secDep.Description.Contains(c, StringComparison.OrdinalIgnoreCase));
                bool isRental = secDep.Description != null &&
                            rentalCategories.Any(c => secDep.Description.Contains(c, StringComparison.OrdinalIgnoreCase));

                // Now apply logic
                if (isCheckedIn)
                {
                    if (isSite)
                        sitesSecurityDepositsApplied += Math.Abs(secDep.Amount ?? 0);
                    else if (isRental)
                        rentalsSecurityDepositsApplied += Math.Abs(secDep.Amount ?? 0);
                }
                else
                {
                    if (isSite)
                        sitesSecurityDepositsTaken += Math.Abs(secDep.Amount ?? 0);
                    else if (isRental)
                        rentalsSecurityDepositsTaken += Math.Abs(secDep.Amount ?? 0);
                }
            }
        */
        if (hasMatchingDate)
        {
            var outputFile = "JsonOutputs/outputTF.txt";

                var arrivedSitesTotal = arrivedSitesList.Sum(p => Math.Abs(p.Amount ?? 0));
                var arrivedRentalsTotal = arrivedRentalsList.Sum(p => Math.Abs(p.Amount ?? 0));
                var confirmedSitesTotal = confirmedSitesList.Sum(p => Math.Abs(p.Amount ?? 0));
                var confirmedRentalsTotal = confirmedRentalsList.Sum(p => Math.Abs(p.Amount ?? 0));

                var summary =
                    $"Arrived Sites ({arrivedSitesList.Count}) (for {startDate:MMM dd yyyy}): {arrivedSitesTotal:C}{Environment.NewLine}" +
                    $"Arrived Rentals ({arrivedRentalsList.Count}) (for {startDate:MMM dd yyyy}): {arrivedRentalsTotal:C}{Environment.NewLine}" +
                    $"Confirmed Sites ({confirmedSitesList.Count}) (for {startDate:MMM dd yyyy}): {confirmedSitesTotal:C}{Environment.NewLine}" +
                    $"Confirmed Rentals ({confirmedRentalsList.Count}) (for {startDate:MMM dd yyyy}): {confirmedRentalsTotal:C}{Environment.NewLine}" +
                    // $"Security Deposits ({securityDepositsHeldList.Count}) (for {startDate:MMM dd yyyy}): {securityDepositsHeldList.Sum(x => x.Amount ?? 0):C}{Environment.NewLine}" +
                    $"Golf Cart Deposits ({golfCartDepositsHeldList.Count}) (for {startDate:MMM dd yyyy}): {golfCartDepositsAppliedTotal}{Environment.NewLine}" +
                    $"Golf Cart Deposits Applied (for {startDate:MMM dd yyyy}): {golfCartDepositsAppliedTotal:C}{Environment.NewLine}" +
                    $"Refund Sites ({refundSitesList.Count}): {refundSitesTotal:C}{Environment.NewLine}" +
                    $"Refund Rentals ({refundRentalsList.Count}): {refundRentalsTotal:C}{Environment.NewLine}" +
                    $"Manual Refund Sites ({refundSitesManualEntry.Count}): {refundSitesManualEntry.Sum(x => x.Amount ?? 0):C}{Environment.NewLine}" +
                    $"Manual Refund Rentals ({refundRentalsManualEntry.Count}): {refundRentalsManualEntry.Sum(x => x.Amount ?? 0):C}{Environment.NewLine}";

                File.WriteAllText(outputFile, string.Empty);
                File.AppendAllText(outputFile, summary);

            deposits.Sites_Deposits_Taken = confirmedSitesList.Sum(p => Math.Abs(p.Amount ?? 0));
            deposits.Sites_Deposits_Applied = siteDepositsAppliedTotal;
            deposits.Sites_Manual_Refunds = refundSitesManualEntry.Sum(x => x.Amount ?? 0);
            deposits.Rentals_Deposits_Taken = confirmedRentalsList.Sum(p => Math.Abs(p.Amount ?? 0));
            deposits.Rentals_Deposits_Applied = rentalDepositsAppliedTotal;
            deposits.Rentals_Manual_Refunds = refundRentalsManualEntry.Sum(x => x.Amount ?? 0);
            deposits.Golf_Cart_Deposits_Taken = golfCartDepositsHeldList.Sum(p => Math.Abs(p.Amount ?? 0));
            deposits.Golf_Cart_Deposits_Applied = golfCartDepositsAppliedTotal;
            deposits.Golf_Cart_Manual_Refunds = refundGolfCarts.Sum(p => Math.Abs(p.Amount ?? 0));
            deposits.Gift_Vouchers_Purchased = giftVouchersPurchases.Sum(p => Math.Abs(p.Amount ?? 0));
            deposits.Gift_Vouchers_Redeemed_For_Sites = giftVouchersSites.Sum(p => Math.Abs(p.Amount ?? 0));
            deposits.Gift_Vouchers_Redeemed_For_Rentals = giftVouchersRentals.Sum(p => Math.Abs(p.Amount ?? 0));
            deposits.Gift_Vouchers_Redeemed_For_Storage = giftVouchersStorage.Sum(p => Math.Abs(p.Amount ?? 0));
        
            // Sites_Deposits_Taken
            depositsList.Add(deposits);
        }
            return depositsList;
            Console.WriteLine("Run Method Complete");
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
            p.Category != null && MatchesCategory(p.Category, categories) &&
            MatchesDeposit(p.Deposit) &&
            MatchesDescription(p.Description));
    }
}
}
