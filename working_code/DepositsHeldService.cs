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
    public class DepositsHeldService : NewbookBaseApi
    {
        private readonly TransactionFlowApi _transactionFlowApi;
        private readonly DailyReport _dailyReport;
        private readonly IDatabaseConnectionService _dbConnectionService;

        public DepositsHeldService(IDatabaseConnectionService dbConnectionService, HttpClient client, ChargesApi chargesApi, DailyReport dailyReport, TransactionFlowApi transactionFlowApi)
            : base(client)
        {
            _dbConnectionService = dbConnectionService;
            _transactionFlowApi = transactionFlowApi;
            _dailyReport = dailyReport;
        }

        public async Task<List<ReservationsDeposits>> ProcessDepositsHeldAsync(DateTime startDate, DateTime endDate)
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
                DepositDate = startDate,
                Sites = 0.0m,
                Mobile_Home_Rentals = 0.0m,
                Rentals = 0.0m,
                Locks_Total = 0.0m,
                Extra_Vehicles = 0.0m,
                Visitor_Fees = 0.0m,
                Manual_Refunds = 0.0m,
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

            string[] siteCategories = { "WESC", "Water & Electric Only" };
            string[] rentalCategories = { "Ocean Villa", "Cottage", "Cabin", "Travel Trailer" };
            string[] extrasVehicles = { "Extra Vehicle", "Vehicle", "Vehicles", "Extra Vehicles Fee", "Extra Vehicle Fees" };

            //START: Retrieve all deposits held for the day for SITES and RENTALS
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

            //START: Fetch Sites data for DBF and Daily Reservations Table
            decimal sitesTotal = 0m;
            sitesTotal = await GetTotals(siteCategories, sitesTotal, checkedInList);
            //END: Fetch Sites data for DBF and Daily Reservations Table

            //START: Fetch Rental data for DBF and Daily Reservations Table
            decimal rentalsTotal = 0m;
            rentalsTotal = await GetTotals(rentalCategories, rentalsTotal, checkedInList);
            //END: Fetch Rental data for DBF and Daily Reservations Table

            var giftVouchersPurchases = transactions
                .Where(r => r.Category == null &&
                r.Description != null &&
                r.Description.Contains("Gift", StringComparison.OrdinalIgnoreCase) &&
                r.PaymentTypeAction != null &&
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

            decimal lockFees = 0.0m;

            if (checkedInList != null && checkedInList.Count > 0)
            {
                foreach (var record in checkedInList)
                {
                    if (!record.TryGetValue("LockFee", out var lockVal) || lockVal == null || lockVal == DBNull.Value)
                        continue;

                    if (record.TryGetValue("LockFee", out var depVal) &&
                        depVal != null &&
                        depVal != DBNull.Value &&
                        decimal.TryParse(Convert.ToString(depVal), out decimal lockFeeVal))
                    {
                        lockFees += Math.Abs(lockFeeVal);
                    }
                }
            }

            // Filter all transactions related to extra vehicles
            var extraVehiclesList = transactions.Where(p => p.Description != null && extrasVehicles.Any(c => p.Description.Contains(c, StringComparison.OrdinalIgnoreCase))).ToList();
            var extraVisitorsTotal = extraVehiclesList.Where(p => p.Amount.HasValue && (p.Amount == -42 || p.Amount == -84)).Sum(p => Math.Abs(p.Amount.Value));
            var extraVehiclesTotal = extraVehiclesList.Where(p => p.Amount.HasValue && (p.Amount == -40 || p.Amount == -80)).Sum(p => Math.Abs(p.Amount.Value));
            bool hasMatchingDate = transactions.Any(p =>
            {
                if (p.TransDate == null) return false;
                var dateValue = Convert.ToDateTime(p.TransDate);
                return dateValue.Date == startDate.Date;
            });

            // Manual Refunds FROM INCOME & NOT VOIDED & LESS THAN 90 DAYS
            var incomeRefundsFinal = noVoidedrefundsIncome
                .Where(r => r.Amount.HasValue && r.Amount > 0 &&
                r.ArrivalDate.HasValue && r.DepartureDate.HasValue &&
                (r.DepartureDate.Value - r.ArrivalDate.Value).TotalDays < 90)
                .ToList();


            foreach (var ite in depositRefunds)
            {
                Console.WriteLine($"depositRefunds: BookingId: {ite.AccountForId}, Amount: {ite.Amount}, TransType: {ite.TransType}, PaymentMethod: {ite.PaymentMethod}");
            }

            foreach (var ite in incomeRefunds)
            {
                Console.WriteLine($"incomeRefunds: BookingId: {ite.AccountForId}, Amount: {ite.Amount}, TransType: {ite.TransType}, PaymentMethod: {ite.PaymentMethod}");
            }  
                
            foreach (var ite in incomeRefundsFinal)
            {
                Console.WriteLine($"incomeRefundsFinal: BookingId: {ite.AccountForId}, Amount: {ite.Amount}, TransType: {ite.TransType}, PaymentMethod: {ite.PaymentMethod}");
            }

            var incomeRefundsSitesAndRentals = incomeRefundsFinal
                .Where(r => r.Category != null &&
                            (siteCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                            rentalCategories.Any(c => r.Category.Contains(c, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            var incomeRefundsSitesAndRentalsTotal = incomeRefundsSitesAndRentals.Sum(r => Math.Abs(r.Amount ?? 0));
            var mrgTotal = incomeRefundsSitesAndRentalsTotal;

            // Asign to the instances.
            if (hasMatchingDate)
            {
                deposits.Sites = sitesTotal;
                deposits.Rentals = rentalsTotal;
                deposits.Locks_Total = lockFees;
                deposits.Extra_Vehicles = extraVehiclesTotal;
                deposits.Visitor_Fees = extraVisitorsTotal;
                deposits.Manual_Refunds = mrgTotal;
                deposits.Sites_Deposits_Taken = confirmedSitesList.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.Sites_Deposits_Applied = siteDepositsAppliedTotal;
                deposits.Sites_Manual_Refunds = refundSitesList.Sum(x => x.Amount ?? 0);
                deposits.Rentals_Deposits_Taken = confirmedRentalsList.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.Rentals_Deposits_Applied = rentalDepositsAppliedTotal;
                deposits.Rentals_Manual_Refunds = refundRentalsList.Sum(x => x.Amount ?? 0);
                deposits.Golf_Cart_Deposits_Taken = golfCartDepositsHeldList.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.Golf_Cart_Deposits_Applied = golfCartDepositsAppliedTotal;
                deposits.Golf_Cart_Manual_Refunds = refundGolfCarts.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.Gift_Vouchers_Purchased = giftVouchersPurchases.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.Gift_Vouchers_Redeemed_For_Sites = giftVouchersSites.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.Gift_Vouchers_Redeemed_For_Rentals = giftVouchersRentals.Sum(p => Math.Abs(p.Amount ?? 0));
                deposits.Gift_Vouchers_Redeemed_For_Storage = giftVouchersStorage.Sum(p => Math.Abs(p.Amount ?? 0));

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
                p.Category != null && MatchesCategory(p.Category, categories) &&
                MatchesDeposit(p.Deposit) &&
                MatchesDescription(p.Description));
        }

        private bool HasCheckedInBeforeDate(int bookingId, DateTime refundDate)
        {
            const string sqlQuery = @"
                SELECT COUNT(1)
                FROM dbo.CheckedIn
                WHERE BookingId = @BookingId
                AND BookingCheckedIn <= @RefundDate";

            using (var sqlConn = _dbConnectionService.CreateConnection())
            using (var cmd = new SqlCommand(sqlQuery, sqlConn))
            {
                cmd.Parameters.Add("@BookingId", SqlDbType.Int).Value = bookingId;
                cmd.Parameters.Add("@RefundDate", SqlDbType.DateTime2).Value = refundDate;

                sqlConn.Open();
                int count = (int)cmd.ExecuteScalar();
                return count > 0;
            }
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

                    // Safely extract decimals
                    decimal depositHeld = 0m;
                    decimal paymentsAfter = 0m;

                    if (record.TryGetValue("DepositsHeld", out var depVal) && depVal != null && depVal != DBNull.Value)
                        decimal.TryParse(Convert.ToString(depVal), out depositHeld);

                    if (record.TryGetValue("PaymentsAfterCheckIn", out var afterVal) && afterVal != null && afterVal != DBNull.Value)
                        decimal.TryParse(Convert.ToString(afterVal), out paymentsAfter);

                    // Only add if at least one of the values is non-zero
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
    }
}