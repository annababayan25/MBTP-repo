using ClosedXML.Excel;
using Spire.Xls;
using Spire.Xls.Core;
using Spire.Xls.Core.Spreadsheet.AutoFilter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using GenericSupport;
using DocumentFormat.OpenXml.Spreadsheet;

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
        static double depositsCarts = 0, depositsChairs = 0, returnedChairs = 0, vouchersSold = 0;
        public static List<NewbookSupport.Revenue> revenueArray;
        public static List<NewbookSupport.Deposits> depositsarray;
        public static List<NewbookSupport.Recon> reconArray;
        public static List<NewbookSupport.Applied> appliedArray;
        public static List<NewbookSupport.Transfers> transfersArray;
        public static List<NewbookSupport.Checks> checksArray;
        public static List<NewbookSupport.SpecialRecon> specialReconArray = new List<NewbookSupport.SpecialRecon>();
        public static Spire.Xls.Worksheet BuildSpecialReconSheet(IXLWorksheet reconSheetIn)
        {
            const int tmpGLCol = 1;
            const int tmpClientCol = 2;
            const int tmpItemCol = 3;
            const int tmpDescCol = 4;
            const int tmpTotalCol = 6;
            Spire.Xls.Workbook spireReconWB = new Spire.Xls.Workbook();
            Spire.Xls.Worksheet spireReconSheet = spireReconWB.Worksheets[0];
            int rowCount = reconSheetIn.LastRowUsed().RowNumber();
            IXLCell workCell;
            string tmpStr, itemToCompare = "", lastClient = "", lastItem = "", lastDesc = "", lastGL = "";
            double runningTotal = 0;
            for (int i = 1; i <= rowCount; i++) // Loop until we find the headers row (usually the first row)
            {
                workCell = reconSheetIn.Row(i).Cell(1);
                if (workCell != null)
                {
                    tmpStr = reconSheetIn.Row(i).Cell(tmpItemCol).Value.ToString();
                    //if (tmpStr.IndexOf("Allocated to Charge") != -1 || tmpStr.IndexOf("Unallocated from Charge") != -1)
                    //{
                    //    itemToCompare = tmpStr.Substring(0, tmpStr.IndexOf("Allocated to Charge") + 9);
                    //}
                    if (tmpStr.IndexOf("Allocated to Charge") != -1)
                    {
                        itemToCompare = tmpStr.Substring(0, tmpStr.IndexOf("Allocated to Charge") + 9);
                    }
                    else if (tmpStr.IndexOf("Unallocated from Charge") != -1)
                    {
                        itemToCompare = tmpStr.Substring(0, tmpStr.IndexOf("Unallocated from Charge") + 11);
                    }
                    else
                    {
                        itemToCompare = tmpStr;
                    }
                }
                if ((lastClient != "" && lastClient != reconSheetIn.Row(i).Cell(tmpClientCol).Value.ToString()) ||
                   (lastItem != "" && lastItem != itemToCompare))
                {
                    if (runningTotal != 0 && lastGL != "1003" && lastGL != "1014" && lastGL != "1016" && lastGL != "1017" && lastGL != "1018")
                    {
                        SpecialRecon tmpRecon = new SpecialRecon() { Gl = lastGL, Client = lastClient, Recon_item = lastItem, Desc = lastDesc, Amount = Math.Round(runningTotal * -1,2) };
                        specialReconArray.Add(tmpRecon);
                    }
                    runningTotal = 0;
                }
                if (reconSheetIn.Row(i).Cell(tmpDescCol).Value.ToString() != "Unallocated Payments" &&
                    reconSheetIn.Row(i).Cell(tmpDescCol).Value.ToString() != "Deposit Payments" &&
                    (reconSheetIn.Row(i).Cell(tmpItemCol).Value.ToString().IndexOf("Allocated to Charge") != -1 ||
                     reconSheetIn.Row(i).Cell(tmpItemCol).Value.ToString().IndexOf("Unallocated from Charge") != -1))
                {
                    lastGL = reconSheetIn.Row(i).Cell(tmpGLCol).Value.ToString();
                    if (lastGL == "362")
                    { 
                        lastGL = "0362"; 
                    }
                    lastClient = reconSheetIn.Row(i).Cell(tmpClientCol).Value.ToString();
                    lastItem = reconSheetIn.Row(i).Cell(tmpItemCol).Value.ToString();
                    if (lastItem.IndexOf("Allocated to Charge") != -1)
                    {
                        lastItem = lastItem.Substring(0, lastItem.IndexOf("Allocated to Charge") + 9);
                    }
                    else if (lastItem.IndexOf("Unallocated from Charge") != -1)
                    {
                        lastItem = lastItem.Substring(0, lastItem.IndexOf("Unallocated from Charge") + 11);

                    }
                    lastDesc = reconSheetIn.Row(i).Cell(tmpDescCol).Value.ToString();
                    double.TryParse(reconSheetIn.Row(i).Cell(tmpTotalCol).Value.ToString(), out double tmpVal); // attempt conversion to double, tmpVal will = 0 if conversion fails
                    runningTotal += tmpVal;
                }
            }
            //foreach (specialRecon item in specialReconArray)
            //{
            //System.Diagnostics.Debug.WriteLine(item.gl + " " + item.recon_item + " " + item.client + " " + item.desc + " " + item.amount.ToString("C"));
            //}
            return spireReconSheet;
        }
        public static string GetMissingCategory(IXLWorksheet sheetIn, string actionIn, string transIn, string clientIn, int catColIn, int transColIn)
        {
            int tmpRowIdx;
            string tmpSearchStr;
            if (transIn.IndexOf("Voided Payments Voided") != -1)
            {
                tmpSearchStr = "Voided Refunds Voided #" + transIn.Substring(transIn.IndexOf("Ref #") + 5).Replace(")", "");
            }
            else if (transIn.IndexOf("Voided Refunds Voided") != -1)
            {
                tmpSearchStr = "Voided Payments Voided #" + transIn.Substring(transIn.IndexOf("Ref #") + 5).Replace(")", "");
            }
            else if (transIn.IndexOf("Refunds Raised") != -1)
            {
                tmpSearchStr = "Payments Raised #" + transIn.Substring(transIn.IndexOf("Ref #") + 5).Replace(")", "");
            }
            else
            {
                tmpSearchStr = "Refunds Raised #" + transIn.Substring(transIn.IndexOf("Ref #") + 5).Replace(")", "");
            }
            tmpRowIdx = 1;
            while (sheetIn.Row(tmpRowIdx).Cell(transColIn).Value.ToString() != "")
            {
                if (sheetIn.Row(tmpRowIdx).Cell(transColIn).Value.ToString().IndexOf(tmpSearchStr) != -1)
                {
                    // NEW IF BLOCK ADDED 9/11/23 2:15 PM
                    if (sheetIn.Row(tmpRowIdx).Cell(transColIn + 2).Value.ToString().IndexOf("Guest") != -1)
                    {
                        return "GUEST";
                    }
                    else
                    {
                        return sheetIn.Row(tmpRowIdx).Cell(catColIn).Value.ToString();
                    }
                }
                tmpRowIdx += 1;
            }
            // If we still haven't found a match try for success using the Booking Chart if it's a Group
            int splitPos = clientIn.IndexOf(" - Split");
            int parenPos = clientIn.IndexOf(")");
            string path__1;
            if (clientIn.IndexOf("Group") != -1)
            {
                path__1 = GenericRoutines.DoesFileExist("", @"Bookings_Chart_", ".xlsx", true);
                if (path__1.IndexOf("FAILURE") == -1)   // The file was found
                {
                    XLWorkbook chartBook = new XLWorkbook(path__1);
                    IXLWorksheet chartSheet = chartBook.Worksheet(1);
                    int rowCount = chartSheet.LastRowUsed().RowNumber();
                    if (splitPos != -1)
                    {
                        tmpSearchStr = clientIn.Substring(parenPos + 2, splitPos - 1 - parenPos) + "(Split)";
                    }
                    else
                    {
                        tmpSearchStr = clientIn;
                    }
                    tmpRowIdx = 2;
                    int chartTransCol = 4;
                    while (tmpRowIdx <= rowCount)
                    {
                        if (chartSheet.Row(tmpRowIdx).Cell(chartTransCol).Value.ToString().IndexOf(tmpSearchStr) != -1)
                        {
                            if (actionIn.IndexOf("Balance Transfer") == -1)
                            {
                                // Move upward through the sheet to find a non-blank row, this will be the category for the split site
                                for (int i = tmpRowIdx; i >= 1; i--)
                                {
                                    if (chartSheet.Row(i).Cell(1).Value.ToString() != "")
                                    {
                                        return chartSheet.Row(i).Cell(1).Value.ToString();
                                    }
                                }
                            }
                        }
                        tmpRowIdx += 1;
                        if (tmpRowIdx > rowCount) { break; }
                    }
                }
                else
                {
                    GenericRoutines.UpdateAlerts(1, "INFORMATIONAL", path__1.Substring(7) + " Not Found, Possible Data Inaccuracy");
                    return "";
                }
            }
            return "";
        }
        public static bool ValidReconEntryFound(string actionIn, string transIn, double amtIn, int transColIn = 0, IXLWorksheet transSheetIn = null)
        {
            if (actionIn.IndexOf("Balance Transf") != -1)
            {
                return false;
            }
            else
            {
                string tmpSearchStr;
                if (transIn.IndexOf("Voided Refunds") != -1)
                {
                    tmpSearchStr = "Refund #" + transIn.Substring(transIn.IndexOf("Voided #") + 8).Replace("Voided ", "") + " Allocated";
                }
                else if (transIn.IndexOf("Refunds Raised") != -1)
                {
                    if(transColIn == 0)
                    {
                        tmpSearchStr = "Payments Raised #" + transIn.Substring(transIn.IndexOf("Ref #") + 5).Replace(")", "");
                    }
                    //tmpSearchStr = "Payments Raised #" + transIn.Substring(transIn.IndexOf("Ref #") + 5).Replace(")", "");
                    else
                    {
                        tmpSearchStr = "Payment " + GetRefundPaymentNumber(transSheetIn, transIn, transColIn) + " Unallocated";
                    }
                }
                else
                {
                    tmpSearchStr = transIn.Substring(transIn.IndexOf("#") + 1, 6);
                    if (tmpSearchStr.IndexOf(" ") == -1)
                    {
                        tmpSearchStr = "Payment #" + tmpSearchStr + " Allocated";
                    }
                    else
                    {
                        tmpSearchStr = "Payment #" + tmpSearchStr.Substring(0, tmpSearchStr.IndexOf(" ") - 1) + " Allocated";
                    }
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
        public static List<Recon> AddRecon(List<Recon> reconArrayIn, string reconCatIn, string transFlowIn, double flowValIn, string glIn = "")
        {
            string modifiedCatIn;
            if (reconCatIn.IndexOf("Visitor") != -1) 
            { 
                modifiedCatIn = "Visitor"; 
            }
            else 
            { 
                modifiedCatIn = reconCatIn; 
            }
            foreach (var item in reconArrayIn)
            {
                if (item.ReconItem.IndexOf(modifiedCatIn) != -1)  // if (item.ReconItem.Substring(0, modifiedCatIn.Length) == modifiedCatIn)
                {
                    item.Accum += flowValIn;
                    if (reconCatIn.IndexOf("Visitor") != -1 || modifiedCatIn == reconCatIn)
                    {
                        AddFlow(item.ReconItem, transFlowIn, flowValIn);
                    }
                    else
                    {
                        AddFlow(reconCatIn, transFlowIn, flowValIn);
                    }
                    return reconArrayIn; //stop processing, work is complete
                }
            }
            Recon newItem = new Recon() { ReconItem = reconCatIn, Accum = flowValIn, GL = glIn, MiscTrans = true};
            reconArrayIn.Add(newItem);
            //AddFlow("ERROR", transFlowIn, flowValIn);
            return reconArrayIn;
        }
        public static void AddAssumption(string _classIn)
        {
            return;
        } // EMPTY STUB
        public static List<Revenue> AddRevenue(List<Revenue> revArrayIn, string transCatIn, string transFlowIn, double flowValIn)
        {
            if (transCatIn != "SKIPPED" && transCatIn != "DROPPED")
            {
                foreach (var item in revArrayIn)
                {
                    if (item.RevType.IndexOf(transCatIn) != -1)
                    {
                        item.Accum += flowValIn;
                        AddFlow(transCatIn, transFlowIn, flowValIn);
                        //System.Diagnostics.Debug.WriteLine(transCatIn + ":" + transFlowIn + " " + flowValIn.ToString("C"));
                        if (transCatIn == "Misc" && (transFlowIn.ToUpper().IndexOf("CHAIR") != -1 ||
                            transFlowIn.ToUpper().IndexOf("CHAIR") != -1))
                        {
                            if (flowValIn > 0)
                            {
                                depositsChairs += flowValIn;
                            }
                            else
                            {
                                returnedChairs += flowValIn;
                            }
                            //item.Accum -= flowValIn; //Back it out since we'll list chairs on their own 925 lines
                        }
                        return revArrayIn; //stop processing, work is complete
                    }
                }
                AddFlow("ERROR REVENUE", transFlowIn, flowValIn); // Only get here it we fell all the way through the foreach loop
                //System.Diagnostics.Debug.WriteLine("THROUGH:" + transFlowIn + " " + flowValIn.ToString("C"));
            }
            else
            {
                AddFlow(transCatIn, transFlowIn, flowValIn);
                //System.Diagnostics.Debug.WriteLine(transCatIn + ":" + transFlowIn + " " + flowValIn.ToString("C"));
            }
            return revArrayIn;
        }
        public static List<Transfers> AddTransfer(List<Transfers> transArrayIn, string xferCatIn, string transFlowIn, double flowValIn)
        {
            foreach (var item in transArrayIn)
            {
                if (item.TranItem.IndexOf(xferCatIn) != -1)
                {
                    item.Accum += flowValIn;
                    AddFlow(item.TranItem, transFlowIn, flowValIn);
                    return transArrayIn; //stop processing, work is complete
                }
            }
            AddFlow("ERROR TRANSFER", transFlowIn, flowValIn);
            return transArrayIn;
        }
        public static List<Applied> AddApplied(List<Applied> appArrayIn, string appCatIn, string transFlowIn, double flowValIn)
        {
            foreach (var item in appArrayIn)
            {
                if (item.AppliedItem.IndexOf(appCatIn) != -1)
                {
                    item.Accum += flowValIn;
                    AddFlow(item.AppliedItem, transFlowIn, flowValIn);
                    //System.Diagnostics.Debug.WriteLine(appCatIn + ":" + transFlowIn + " " + flowValIn.ToString("C"));
                    return appArrayIn; //stop processing, work is complete
                }
            }
            AddFlow("ERROR APPLIED", transFlowIn, flowValIn);
            return appArrayIn;
        }
        public static List<Deposits> AddDeposit(List<Deposits> depArrayIn, string depCatIn, int arrayPosIn, string transFlowIn, double flowValIn)
        {
            string depStr;
            if (depCatIn == "Golf")
            {
                depArrayIn[arrayPosIn].GolfAccum += flowValIn;
                depStr = "Golf Deposits(FY" + depArrayIn[arrayPosIn].Fy.ToString("yy") + ")";
                depositsCarts += flowValIn;
            }
            else if (depCatIn == "Vouchers")
            {
                depArrayIn[arrayPosIn].VouchersAccum += flowValIn;
                depStr = "Vouchers Sold";
                vouchersSold += flowValIn;
            }
            else
            {
                if (depCatIn.IndexOf("WESC") != -1)
                {
                    depArrayIn[arrayPosIn].WescAccum += flowValIn;
                    depStr = "Campsite Deposits(FY" + depArrayIn[arrayPosIn].Fy.ToString("yy") + ")";
                }
                else if (depCatIn.IndexOf("Rentals") != -1)
                {
                    depArrayIn[arrayPosIn].RentalAccum += flowValIn;
                    depStr = "Rental Unit Deposits(FY" + depArrayIn[arrayPosIn].Fy.ToString("yy") + ")";
                }
                else { depStr = "ERROR DEPOSIT"; }
            }
            AddFlow(depStr, transFlowIn, flowValIn);
            return depArrayIn;
        }
        public static void AddFlow(string assignedIn, string actionIn, double amtIn)
        {
            string assignedParam = assignedIn + ":";
            string bookingParam = actionIn.Substring(actionIn.IndexOf("Booking") + 9, 6);
            if (actionIn.IndexOf("Booking") == -1)
            {
                bookingParam = "GUEST";
            }
            if(Math.Abs(amtIn) == 30.24)
            //if(assignedIn.IndexOf("Golf") != -1)
            //if(assignedIn.IndexOf("GolfDepApp") != -1 || assignedIn.IndexOf("GolfCartRentals") != -1)
            //if (actionIn.IndexOf("344287") != -1 || actionIn.IndexOf("352158") != -1)
            {
                System.Diagnostics.Debug.WriteLine(assignedParam + actionIn + " " + amtIn.ToString("C") + " " + bookingParam);
            }
            return;
        }
        public static List<Checks> AddCheck(List<Checks> checkArrayIn, string checkCatIn, string checkFlowIn, double flowValIn)
        {
            foreach (var item in checkArrayIn)
            {
                if (item.CheckItem.IndexOf(checkCatIn) != -1)
                {
                    item.Accum += flowValIn;
                    AddFlow(item.CheckItem, checkFlowIn, flowValIn);
                    return checkArrayIn; //stop processing, work is complete
                }
            }
            AddFlow("ERROR CHECK", checkFlowIn, flowValIn);
            return checkArrayIn;
        } 
        public static int CheckForCancel(string idToCheck)
        {
            string path__1 = GenericRoutines.DoesFileExist("", @"Booking_Adjustments_", ".xlsx", true);
            if (path__1.IndexOf("FAILURE") == -1) // file found
            {
                if (idToCheck.IndexOf("Booking") != -1) { idToCheck = idToCheck.Substring(10, 6); }
                XLWorkbook adjustBook = new XLWorkbook(path__1);
                IXLWorksheet adjustSheet = adjustBook.Worksheet(1);
                IXLAutoFilter myFilter = adjustSheet.Range(adjustSheet.Cell(2, 1), adjustSheet.LastCellUsed()).SetAutoFilter(true);
                myFilter.Column(1).EqualTo("Bookings #" + idToCheck);
                myFilter.Column(2).EqualTo("Status");
                myFilter.Column(3).EqualTo("Confirmed");
                myFilter.Column(4).EqualTo("Cancelled");
                if (myFilter.VisibleRows.Count() != 1) { return 1; } else { return 0; }
            }
            else
            {
                GenericRoutines.UpdateAlerts(1, "FATAL ERROR", path__1.Substring(7) + " Not Found, IMPORT ABORTED");
                return -1;
            }
        }
        public static bool CheckFYChange(string idToCheck)
        {
            string path__1 = GenericRoutines.DoesFileExist("", @"Booking_Adjustments_", ".xlsx", true);
            if (path__1.IndexOf("FAILURE") == -1) // file found
            {
                if (idToCheck.IndexOf("Booking") != -1) { idToCheck = idToCheck.Substring(11, 6); }
                XLWorkbook adjustBook = new XLWorkbook(path__1);
                IXLWorksheet adjustSheet = adjustBook.Worksheet(1);
                IXLAutoFilter myFilter = adjustSheet.Range(adjustSheet.Cell(2, 1), adjustSheet.LastCellUsed()).SetAutoFilter(true);
                myFilter.Column(1).EqualTo("Bookings #" + idToCheck); // filter for specific booking
                myFilter.Column(2).EqualTo("Period To");
                if (myFilter.VisibleRows.Count() != 0) { return true; } else { return false; }
            }
            else
            {
                GenericRoutines.UpdateAlerts(1, "FATAL ERROR", path__1.Substring(7) + " Not Found, IMPORT ABORTED");
                return false;
            }
        }
        public static string PaymentRaised(IXLWorksheet tmpSheet, string glIn, string paymentIn, double pymtValIn, string itemCheck,
                       int startIn)
        {
            for (int i = startIn; i <= tmpSheet.LastRowUsed().RowNumber(); i++) // Loop to the end of the file or until match is found
            {
                double.TryParse(tmpSheet.Row(i).Cell(8).Value.ToString(), out double amtVal); // attempt conversion to double, ignore if false (cellVal will = 0)
                if (tmpSheet.Row(i).Cell(1).Value.ToString().IndexOf("Balance Transfer") == -1 &&
                    tmpSheet.Row(i).Cell(3).Value.ToString().IndexOf(paymentIn) != -1 &&
                    (Math.Round(amtVal, 2) == Math.Round(pymtValIn, 2) ||
                     (Math.Round(amtVal, 2) == (Math.Round(pymtValIn, 2) * -1) && itemCheck.IndexOf("Unallocated") != -1)))
                {
                    if (glIn == "0361")
                    {
                        if (tmpSheet.Row(i).Cell(3 - 1).Value.ToString().IndexOf("Annual") != -1)
                        {
                            if (tmpSheet.Row(i).Cell(7).Value.ToString().IndexOf("VEHICLE") != -1)
                            {
                                return "ANNUAL - MISC";
                            }
                            else
                            {
                                return "ANNUAL";
                            }
                        }
                        else
                        {
                            if (tmpSheet.Row(i).Cell(7).Value.ToString().IndexOf("VEHICLE") != -1)
                            {
                                return "MOBILE - MISC";
                            }
                            else
                            {
                                return "MOBILE";
                            }
                        }
                    }
                    else
                    {
                        return "OK";
                    }
                }
            }
            return "NO";
        } // END OF PaymentRaised
        public static string GetRefundPaymentNumber(IXLWorksheet sheetIn, string transIn, int transColIn)
        {
            int tmpRowIdx = 1; ;
            string tmpSearchStr = transIn.Substring(transIn.IndexOf("Ref #")).Replace(")", "");
            while (sheetIn.Row(tmpRowIdx).Cell(transColIn).Value.ToString() != "")
            {
                if (sheetIn.Row(tmpRowIdx).Cell(transColIn).Value.ToString().IndexOf(tmpSearchStr) != -1)
                {
                    int hashPos = sheetIn.Row(tmpRowIdx).Cell(transColIn).Value.ToString().IndexOf("#");
                    return sheetIn.Row(tmpRowIdx).Cell(transColIn).Value.ToString().Substring(hashPos, 7);
                }
                tmpRowIdx += 1;
            }
            return "#999999";
        }
    }
}