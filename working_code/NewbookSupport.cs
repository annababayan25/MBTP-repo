using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Interfaces;
using MBTP.Models;
using GenericSupport;
using ClosedXML.Excel;


namespace NewbookSupport
{
    public class Revenue
    {
#nullable enable
        public string? RevType { get; set; }
        public double Accum { get; set; }
    }

    public class Deposits
    {
        public DateTime Fy { get; set; }
        public double WescAccum { get; set; }
        public double RentalAccum { get; set; }
        public double GolfAccum { get; set; }
        public double VouchersAccum { get; set; }
    }

    public class Recon
    {
        public string? ReconItem { get; set; }
        public double Accum { get; set; }
        public string? GL { get; set; }
        public bool MiscTrans { get; set; }
    }

    public class Applied
    {
        public string? AppliedItem { get; set; }
        public double Accum { get; set; }
    }

    public class Transfers
    {
        public string? TranItem { get; set; }
        public double Accum { get; set; }
    }

    public class Checks
    {
        public string? CheckItem { get; set; }
        public double Accum { get; set; }
    }

    public class SpecialRecon
    {
        public string? Gl { get; set; }
        public string? Client { get; set; }
        public string? Recon_item { get; set; }
        public string? Desc { get; set; }
        public double Amount { get; set; }
    }
 #nullable disable
   
    public class SupportRoutines
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public SupportRoutines(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
       static double depositsCarts = 0, depositsChairs = 0, returnedChairs = 0, vouchersSold = 0;
        public static List<NewbookSupport.Revenue> revenueArray;
        public static List<NewbookSupport.Deposits> depositsarray;
        public static List<NewbookSupport.Recon> reconArray;
        public static List<NewbookSupport.Applied> appliedArray;
        public static List<NewbookSupport.Transfers> transfersArray;
        public static List<NewbookSupport.Checks> checksArray;
        public static List<NewbookSupport.SpecialRecon> specialReconArray = new List<NewbookSupport.SpecialRecon>();
        public static List<SpecialRecon> BuildSpecialReconList(List<ReconsApi> recons)
        {
            var result = new List<SpecialRecon>();

            string lastGL = "";
            string lastItem = "";
            double runningTotal = 0;

            foreach (var row in recons
                .Where(r => !string.IsNullOrEmpty(r.ItemDescription))
                .OrderBy(r => r.ItemDescription)
                .ThenBy(r => r.GLAccountCode))
            {
                string itemToCompare;

                if (row.ItemDescription!.Contains("Allocated to Charge"))
                {
                    itemToCompare = row.ItemDescription.Substring(
                        0, row.ItemDescription.IndexOf("Allocated to Charge") + 9);
                }
                else if (row.ItemDescription.Contains("Unallocated from Charge"))
                {
                    itemToCompare = row.ItemDescription.Substring(
                        0, row.ItemDescription.IndexOf("Unallocated from Charge") + 11);
                }
                else
                {
                    itemToCompare = row.ItemDescription;
                }

                // Group break
                if (lastItem != "" && lastItem != itemToCompare)
                {
                    if (runningTotal != 0 &&
                        lastGL != "1003" && lastGL != "1014" &&
                        lastGL != "1016" && lastGL != "1017" &&
                        lastGL != "1018")
                    {
                        result.Add(new SpecialRecon
                        {
                            Gl = lastGL,
                            Recon_item = lastItem,
                            Amount = Math.Round(runningTotal * -1, 2)
                        });
                    }

                    runningTotal = 0;
                }

                lastGL = row.GLAccountCode == "362"
                    ? "0362"
                    : row.GLAccountCode;

                lastItem = itemToCompare;
                runningTotal += (double)(row.Total_TaxInc ?? 0);
            }

            return result;
        }



        #region Category Lookup
        public static string GetMissingCategory(List<TransactionFlow> transactions, string actionIn, string transIn, string clientIn, DateTime reportDate)
        {
            string tmpSearchStr = BuildSearchString(transIn);

            if (string.IsNullOrEmpty(tmpSearchStr))
            {
                return "";
            }

            for (int i = 0; i < transactions.Count; i++)
            {
                var transaction = transactions[i];

                if (transaction.FormattedTransNumber?.Contains(tmpSearchStr) == true)
                {

                    if (transaction.ClientAccount?
                        .Contains("Guest", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return "GUEST";
                    }

                    if (!string.IsNullOrEmpty(transaction.Category))
                    {
                        return transaction.Category;
                    }

                    // Group fallback
                    if (clientIn.Contains("Group", StringComparison.OrdinalIgnoreCase) &&
                        !actionIn.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                    {
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (!string.IsNullOrWhiteSpace(transactions[j].Category))
                            {
                                return transactions[j].Category;
                            }
                        }

                    }

                }
            }

            if (clientIn.Contains("Group", StringComparison.OrdinalIgnoreCase))
            {
                return GetCategoryFromBookingChart(actionIn, clientIn, reportDate);
            }

            return "";
        }


        private static string GetCategoryFromBookingChart(string actionIn, string clientIn, DateTime reportDate)
        {
            int splitPos = clientIn.IndexOf(" - Split", StringComparison.OrdinalIgnoreCase);
            int parenPos = clientIn.IndexOf(")");

            string path = GenericRoutines.DoesFileExist("", @"Bookings_Chart_", ".xlsx", true);
            if (path.Contains("FAILURE"))
            {
                GenericRoutines.UpdateAlerts2(
                    1,
                    "INFORMATIONAL",
                    path.Substring(7) + " Not Found, Possible Data Inaccuracy", reportDate);

                return "";
            }

            using var chartBook = new XLWorkbook(path);
            var chartSheet = chartBook.Worksheet(1);

            int rowCount = chartSheet.LastRowUsed().RowNumber();
            int chartTransCol = 4;

            string tmpSearchStr;
            if (splitPos != -1 && parenPos != -1)
            {
                tmpSearchStr =
                    clientIn.Substring(parenPos + 2, splitPos - 1 - parenPos) + "(Split)";
            }
            else
            {
                tmpSearchStr = clientIn;
            }

            for (int row = 2; row <= rowCount; row++)
            {
                if (chartSheet.Row(row).Cell(chartTransCol)
                    .GetString()
                    .Contains(tmpSearchStr, StringComparison.OrdinalIgnoreCase))
                {
                    if (!actionIn.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase))
                    {
                        // Walk UP to find first non-blank category (column 1)
                        for (int i = row; i >= 1; i--)
                        {
                            string category = chartSheet.Row(i).Cell(1).GetString();
                            if (!string.IsNullOrWhiteSpace(category))
                                return category;
                        }
                    }
                }
            }

            return "";
        }


        private static string BuildSearchString(string transIn)
        {
            int refIndex = transIn.IndexOf("Ref #");
            if (refIndex == -1)
                return "";

            string refNumber = transIn.Substring(refIndex + 5).Replace(")", "");

            if (transIn.Contains("Voided Payments Voided"))
                return "Voided Refunds Voided #" + refNumber;
            
            if (transIn.Contains("Voided Refunds Voided"))
                return "Voided Payments Voided #" + refNumber;
            
            if (transIn.Contains("Refunds Raised"))
                return "Payments Raised #" + refNumber;
            
            return "Refunds Raised #" + refNumber;
        }
        #endregion

        #region Recon Validation
            
        public static bool ValidReconEntryFound(string actionIn, TransactionFlow transaction, double amtIn, int transColIn = 0)
        {
            if (transaction.TranslatedPaymentType.Contains("Balance Transfer"))
            {
                return false;
            }
            else
            {
                string tmpSearchStr;
                if (transaction.TransType.Contains("Voided Refunds"))
                {
                    tmpSearchStr =
                        "Refund #" +
                        transaction.PaymentTypeReference +
                        " Allocated";
                }
                else if (transaction.TransType.Contains("Refunds Raised"))
                {
                    if (transColIn == 0)
                    {
                        tmpSearchStr =
                            "Payments Raised #" +
                            transaction.PaymentTypeReference;
                    }
                    else
                    {
                        tmpSearchStr =
                            "Payment " +
                            GetRefundPaymentNumber(transaction) +
                            " Unallocated";
                    }
                }
                else
                {
                    tmpSearchStr =
                        "Payment #" +
                        transaction.PaymentTypeReference +
                        " Allocated";
                }

                int combRowCnt = 0;
                int i;
                foreach (SpecialRecon item in specialReconArray)
                {
                    i = 0;
                    if (item.Recon_item == tmpSearchStr)
                    {
                        if ((item.Gl.Substring(0, 1) == "0" || item.Gl == "288" || item.Gl.Substring(0, 4) == "1018" || item.Gl.Substring(0, 4) == "1020") && (Math.Round(item.Amount, 2) == amtIn))
                        {
                            return true; // valid entry, look no further
                        }
                        else
                        {
                            int[] tmpCombRowArray = new int[specialReconArray.Count + 1];
                            tmpCombRowArray[0] = i;
                            //for (int j = 1; j <= 4; j++)
                            for (int j = 1; j <= specialReconArray.Count; j++)
                            {
                                tmpCombRowArray[j] = 0;
                            }
                            //if (descIn.ToUpper().IndexOf("OYSTER") != -1)
                            //{
                            //    return false;
                            //}
                            double tmpReconSum = Math.Round(item.Amount, 2);
                            combRowCnt++;
                            int ii = 0;
                            foreach (SpecialRecon item2 in specialReconArray)
                            {
                                if (ii <= i)
                                {
                                    continue;
                                }
                                else
                                {
                                    if (item2.Gl.Substring(0, 1) == "0" || item2.Gl.Substring(0, 4) == "1018" || item2.Gl.Substring(0, 4) == "1020")
                                    {
                                        if (Math.Round(item2.Amount, 2) == amtIn)
                                        {
                                            return true; // valid entry, look no further
                                        }
                                        else
                                        {
                                            tmpReconSum = Math.Round(item2.Amount, 2);
                                            combRowCnt++;
                                            tmpCombRowArray[combRowCnt] = ii;
                                        }
                                    }
                                }
                                ii++;
                            }
                        }
                    }
                }
                return false;
            }
        }

        private static string BuildReconSearchString(string transIn)
        {
            if (transIn.Contains("Voided Refunds"))
            {
                int voidedIndex = transIn.IndexOf("Voided #");
                if (voidedIndex != -1)
                    return "Refund #" + transIn.Substring(voidedIndex + 8).Replace("Voided ", "") + " Allocated";
            }
            else if (transIn.Contains("Refunds Raised"))
            {
                int refIndex = transIn.IndexOf("Ref #");
                if (refIndex != -1)
                    return "Payments Raised #" + transIn.Substring(refIndex + 5).Replace(")", "");
            }
            else
            {
                int hashIndex = transIn.IndexOf("#");
                if (hashIndex != -1 && hashIndex + 7 <= transIn.Length)
                {
                    string paymentNum = transIn.Substring(hashIndex + 1, Math.Min(6, transIn.Length - hashIndex - 1));
                    int spaceIndex = paymentNum.IndexOf(" ");
                    
                    if (spaceIndex != -1)
                        paymentNum = paymentNum.Substring(0, spaceIndex);
                    
                    return "Payment #" + paymentNum + " Allocated";
                }
            }

            return "";
        }
        #endregion

        #region Payment Validation
        public static string PaymentRaised(List<TransactionFlow> transactions, string glIn, string paymentIn, 
                                          double pymtValIn, string itemCheck )
        {
            foreach (var transaction in transactions)
            {
                if (!transaction.Amount.HasValue)
                    continue;

                double amtVal = (double)transaction.Amount.Value;

                bool isNotBalanceTransfer = !(transaction.PaymentTypeAction?.Contains("Balance Transfer") ?? false);
                bool paymentMatches = transaction.TransType?.Contains(paymentIn) ?? false;
                bool amountMatches = Math.Round(amtVal, 2) == Math.Round(pymtValIn, 2) ||
                                   (Math.Round(amtVal, 2) == (Math.Round(pymtValIn, 2) * -1) &&
                                    itemCheck?.Contains("Unallocated") == true);

                if (isNotBalanceTransfer && paymentMatches && amountMatches)
                {
                    if (glIn == "0361")
                    {
                        bool isAnnual = transaction.Category?.Contains("Annual") ?? false;
                        bool hasVehicle = transaction.Description?.Contains("VEHICLE") ?? false;

                        return isAnnual 
                            ? (hasVehicle ? "ANNUAL - MISC" : "ANNUAL")
                            : (hasVehicle ? "MOBILE - MISC" : "MOBILE");
                    }
                    
                    return "OK";
                }
            }

            return "NO";
        }
        #endregion

        #region Array Update Methods
        public static List<Recon> AddRecon(List<Recon> reconArray, string reconCat, string transFlow, 
                                          double flowVal, string gl = "")
        {
            string modifiedCat = reconCat.Contains("Visitor") ? "Visitor" : reconCat;

            foreach (var item in reconArray)
            {
                if (item.ReconItem?.Contains(modifiedCat) == true)
                {
                    item.Accum += flowVal;
                    AddFlow(reconCat.Contains("Visitor") || modifiedCat == reconCat ? item.ReconItem : reconCat, 
                           transFlow, flowVal);
                    return reconArray;
                }
            }

            reconArray.Add(new Recon 
            { 
                ReconItem = reconCat, 
                Accum = flowVal, 
                GL = gl, 
                MiscTrans = true 
            });
            
            return reconArray;
        }

        public static List<Revenue> AddRevenue(List<Revenue> revArray, string transCat, string transFlow, double flowVal)
        {
            if (transCat == "SKIPPED" || transCat == "DROPPED")
            {
                AddFlow(transCat, transFlow, flowVal);
                return revArray;
            }

            foreach (var item in revArray)
            {
                if (item.RevType?.Contains(transCat) == true)
                {
                    item.Accum += flowVal;
                    AddFlow(transCat, transFlow, flowVal);
                    return revArray;
                }
            }

            AddFlow("ERROR REVENUE", transFlow, flowVal);
            return revArray;
        }

        public static List<Transfers> AddTransfer(List<Transfers> transArray, string xferCat, string transFlow, double flowVal)
        {
            foreach (var item in transArray)
            {
                if (item.TranItem?.Contains(xferCat) == true)
                {
                    item.Accum += flowVal;
                    AddFlow(item.TranItem, transFlow, flowVal);
                    return transArray;
                }
            }

            AddFlow("ERROR TRANSFER", transFlow, flowVal);
            return transArray;
        }

        public static List<Applied> AddApplied(List<Applied> appArray, string appCat, string transFlow, double flowVal)
        {
            foreach (var item in appArray)
            {
                if (item.AppliedItem?.Contains(appCat) == true)
                {
                    item.Accum += flowVal;
                    AddFlow(item.AppliedItem, transFlow, flowVal);
                    return appArray;
                }
            }

            AddFlow("ERROR APPLIED", transFlow, flowVal);
            return appArray;
        }

        public static List<Deposits> AddDeposit(List<Deposits> depArray, string depCat, int arrayPos, 
                                               string transFlow, double flowVal)
        {
            string depStr;

            switch (depCat)
            {
                case "Golf":
                    depArray[arrayPos].GolfAccum += flowVal;
                    depStr = $"Golf Deposits(FY{depArray[arrayPos].Fy:yy})";
                    break;

                case "Vouchers":
                    depArray[arrayPos].VouchersAccum += flowVal;
                    depStr = "Vouchers Sold";
                    break;

                default:
                    if (depCat.Contains("WESC"))
                    {
                        depArray[arrayPos].WescAccum += flowVal;
                        depStr = $"Campsite Deposits(FY{depArray[arrayPos].Fy:yy})";
                    }
                    else if (depCat.Contains("Rentals"))
                    {
                        depArray[arrayPos].RentalAccum += flowVal;
                        depStr = $"Rental Unit Deposits(FY{depArray[arrayPos].Fy:yy})";
                    }
                    else
                    {
                        depStr = "ERROR DEPOSIT";
                    }
                    break;
            }

            AddFlow(depStr, transFlow, flowVal);
            return depArray;
        }

        public static List<Checks> AddCheck(List<Checks> checkArray, string checkCat, string checkFlow, double flowVal)
        {
            foreach (var item in checkArray)
            {
                if (item.CheckItem?.Contains(checkCat) == true)
                {
                    item.Accum += flowVal;
                    AddFlow(item.CheckItem, checkFlow, flowVal);
                    return checkArray;
                }
            }

            AddFlow("ERROR CHECK", checkFlow, flowVal);
            return checkArray;
        }
        #endregion

        #region Helper Methods
        public static void AddAssumption(string classIn)
        {
            return;// Stub for future implementation
        }

        public static void AddFlow(string assignedIn, string actionIn, double amtIn)
        {
            string assignedParam = assignedIn + ":";
            string bookingParam = actionIn.Substring(actionIn.IndexOf("Booking") + 9, 6);
            if (actionIn.IndexOf("Booking") == -1)
            {
                bookingParam = "GUEST";
            }
            //if(Math.Abs(amtIn) == 30.24)
            //if(assignedIn.IndexOf("Golf") != -1)
            //if(assignedIn.IndexOf("GolfDepApp") != -1 || assignedIn.IndexOf("GolfCartRentals") != -1)
            //if (actionIn.IndexOf("344287") != -1 || actionIn.IndexOf("352158") != -1)
            //{
            //    System.Diagnostics.Debug.WriteLine(assignedParam + actionIn + " " + amtIn.ToString("C") + " " + bookingParam);
            //}
            return;
        }
    
       public int CheckForCancel(int idToCheck, DateTime reportDate)
        {
            string sqlQuery = @"
                SELECT BookingCancelled
                FROM dbo.Bookings
                WHERE BookingId = @idToCheck
            ";

            try
            {
                using (var sqlConn = _dbConnectionService.CreateConnection())
                using (var cmd = new SqlCommand(sqlQuery, sqlConn))
                {
                    cmd.Parameters.Add("@idToCheck", SqlDbType.Int).Value = idToCheck;

                    sqlConn.Open();

                    object result = cmd.ExecuteScalar();

                    // No record found
                    if (result == null || result == DBNull.Value)
                    {
                        return 0; // treat as not cancelled
                    }

                    string bookingCancelled = result.ToString();

                    return bookingCancelled != null ? 1 : 0;
                }
            }
            catch (Exception ex)
            {
                GenericRoutines.UpdateAlerts2(
                    1,
                    "FATAL ERROR",
                    $"API Request for CheckForCancel failed: {ex.Message}",
                    reportDate
                );
                return -1;
            }
        }

        public bool CheckFYChange(int bookingId, DateTime reportDate)
        {
            string sqlQuery = @"
                SELECT BookingArrival, BookingDeparture
                FROM dbo.Bookings
                WHERE BookingId = @bookingId
            ";

            try
            {
                using (var sqlConn = _dbConnectionService.CreateConnection())
                using (var cmd = new SqlCommand(sqlQuery, sqlConn))
                {
                    cmd.Parameters.Add("@bookingId", SqlDbType.Int).Value = bookingId;

                    sqlConn.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            // Booking not found → no FY change
                            return false;
                        }

                        DateTime arrival = reader.GetDateTime(0);
                        DateTime departure = reader.GetDateTime(1);

                        int arrivalFY = GetFinancialYear(arrival);
                        int departureFY = GetFinancialYear(departure);

                        return arrivalFY != departureFY;
                    }
                }
            }
            catch (Exception ex)
            {
                GenericRoutines.UpdateAlerts2(
                    1,
                    "FATAL ERROR",
                    $"API Request for CheckFYChange failed: {ex.Message}",
                    reportDate
                );
                return false;
            }
        }

        private static int GetFinancialYear(DateTime date)
        {
            // Fiscal year starts October 1
            return date.Month < 10
                ? date.Year
                : date.Year + 1;
        }

        public static string GetRefundPaymentNumber(TransactionFlow transaction)
        {
            if (string.IsNullOrWhiteSpace(transaction?.PaymentTypeReference))
                return "#999999";

            return "#" + transaction.PaymentTypeReference;
        }

        #endregion
    }
}