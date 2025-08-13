using System;
using DocumentFormat.OpenXml;
using System.Data;
using ClosedXML.Excel;
using Spire.Xls.Core;
using GenericSupport;
using SQLStuff;
using MBTP.Interfaces;

namespace FinancialC_
{
    public class POSImports
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public POSImports(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
        public void ReadArcadeFiles()
        {
            const int taxCol = 6;
            const int payCol = 5;
            double ccSum = 0;
            double cashSum = 0;
            double voucherSum = 0;
            string matchDate = GenericRoutines.repDateTmp.ToString("M/d/yyyy");

            // Verify that all files exist.  If any are missing there is no point in processing further.
            if (!GenericRoutines.AllFilesPresent(3)) { return; }
 
             // Create the connection to the database and define the SQl command that calls the stored procedure.  Stop here it there's a problem
            SQLSupport sqlSupport = new SQLSupport(_dbConnectionService);
            if (!sqlSupport.PrepareForImport("UpdateArcadeTable"))
            {
                return;
            }

            try
            {
                XLWorkbook workBook = new XLWorkbook(GenericRoutines.registerFiles.Sales);
                IXLWorksheet workSheet = workBook.Worksheet(1);
                IXLCell workCell;
                int totalRow = workSheet.LastRowUsed()!.RowNumber();
                int totalCol = workSheet.LastColumnUsed()!.ColumnNumber();
                // grab the sales report header to verify that it's not the wrong report with the right name
                workCell = workSheet.Row(1).Cell(1);
                if (workCell is null || !workCell.Value.ToString().Contains("SALES BY DAY REPORT"))
                {
                    GenericRoutines.UpdateAlerts(3, "FATAL ERROR", GenericRoutines.registerFiles.Sales.Substring(7) + " Is Not The Correct Report, IMPORT ABORTED");
                    return;
                }
                // get the row containing sales numbers for the day
                workCell = workSheet.Row(totalRow).Cell(1);
                if (workCell != null && workCell.Value.ToString().Substring(0, workCell.Value.ToString().IndexOf(" ")) == matchDate)  // Makes sure the date matches the day we're looking for
                {
                    workCell = workSheet.Row(totalRow).Cell(totalCol); // grab the total sales
                    double.TryParse(workCell.Value.ToString(), out double cellVal); // attempt conversion to double, ignore if false (cellVal will = 0)
                    sqlSupport.AddSQLParameter("@PreparedFood", SqlDbType.SmallMoney, cellVal); // add parameter for stored procedure
                    workCell = workSheet.Row(totalRow).Cell(taxCol); // grab the total tax collected
                    double.TryParse(workCell.Value.ToString(), out cellVal); // attempt conversion to double, ignore if false (cellVal will = 0)
                    sqlSupport.AddSQLParameter("@TotalTaxCollected", SqlDbType.SmallMoney, cellVal); // add parameter for stored procedure

                    // Next read the Payments file
                    workBook = new XLWorkbook(GenericRoutines.registerFiles.Payments);
                    workSheet = workBook.Worksheet(1);
                    int rowCount = workSheet.LastRowUsed()!.RowNumber();
                    // grab the payments report header to verify that it's not the wrong report with the right name
                    workCell = workSheet.Row(1).Cell(1);
                    if (workCell is null || !workCell.Value.ToString().Contains("PAYMENT SUMMARY REPORT"))
                    {
                        GenericRoutines.UpdateAlerts(3, "FATAL ERROR", GenericRoutines.registerFiles.Payments.Substring(7) + " Is Not The Correct Report, IMPORT ABORTED");
                        return;
                    }
                    //read the worksheet rows until the column headers line is reached
                    for (int i = 1; i <= rowCount; i++)
                    {
                        workCell = workSheet.Row(i).Cell(1);
                        string str = workCell.Value.ToString();
                        if (workCell != null && (str == "Credit Card" || str == "Cash" || str == "Gift Certificate"))
                        {
                            double.TryParse(workSheet.Row(i).Cell(payCol).Value.ToString(), out cellVal); // attempt conversion to double, ignore if false (cellVal will = 0)
                            if (str == "Credit Card")
                            {
                                ccSum += cellVal;
                            }
                            else if (str == "Cash")
                            {
                                cashSum += cellVal;
                            }
                            else
                            {
                                voucherSum += cellVal;
                            }
                        }
                    }
                    sqlSupport.AddSQLParameter("@ArcadeCC", SqlDbType.SmallMoney, ccSum);
                    sqlSupport.AddSQLParameter("@ArcadeCash", SqlDbType.SmallMoney, cashSum);
                    sqlSupport.AddSQLParameter("@ArcadeVouchers", SqlDbType.SmallMoney, voucherSum);
                    // act on the transaction table
                    _ = sqlSupport.ExecuteStoredProcedure(3);
                }
                else
                {
                    GenericRoutines.UpdateAlerts(3, "FATAL ERROR", GenericRoutines.registerFiles.Sales.Substring(7) + " Is Not The Correct Report, IMPORT ABORTED");
                    return;
                }
            }
            catch (Exception ex)
            {
                GenericRoutines.UpdateAlerts(3, "FATAL ERROR",  ex.ToString() + ", IMPORT ABORTED");
                return;
            }
        }
        public void ReadCoffeeFiles()
        {
            // declare constants
            const int catCol = 3;
            const int salesCol = 8;
            const int taxCol = 12;
            const int modsalesCol = 5;
            const int payCol = 5;
            double preparedSum = 0;
            double foodSum = 0;
            double otherSum = 0;
            double taxSum = 0;
            double ccSum = 0;
            double cashSum = 0;

            // Verify that all files exist.  If any are missing there is no point in processing further.
            if (!GenericRoutines.AllFilesPresent(4)) { return; }

            // Create the connection to the database and define the SQl command that calls the stored procedure.  Stop here it there's a problem
            SQLSupport sqlSupport = new SQLSupport(_dbConnectionService);
            if (!sqlSupport.PrepareForImport("UpdateCoffeeTable")) { return; }

            try
            {
                XLWorkbook workBook = new XLWorkbook(GenericRoutines.registerFiles.Sales);
                IXLWorksheet workSheet = workBook.Worksheet(1);
                IXLCell workCell;
                // grab the sales report header to verify that it's not the wrong report with the right name
                workCell = workSheet.Row(1).Cell(1);
                if (workCell is null || !workCell.Value.ToString().Contains("TOP ITEM SALES REPORT"))
                {
                    GenericRoutines.UpdateAlerts(4, "FATAL ERROR", GenericRoutines.registerFiles.Sales.Substring(7) + " Is Not The Correct Report, IMPORT ABORTED");
                    return;
                }
                int rowCount = workSheet.LastRowUsed()!.RowNumber();
                double cellVal, workingSum;
                string str;
                string numStr;
                bool catFound = false;
                //iterate over the worksheet rows to accumulate the sales data by category
                for (int i = 1; i <= rowCount; i++) // Stop 1 row short of the end, that's the Grand Total row
                {
                    workCell = workSheet.Row(i).Cell(catCol);
                    if (workCell != null)
                    {
                        str = workCell.Value.ToString(); // grab item category
                        if (catFound)
                        {
                            workingSum = 0;
                            for (int k = salesCol; k <= taxCol; k++) // iterate over the colums to calculate total sales sum for the item
                            {
                                numStr = workSheet.Row(i).Cell(k).Value.ToString();
                                if (double.TryParse(numStr, out cellVal)) // attempt conversion to double, ignore if false (cellVal will = 0)
                                {
                                    workingSum += cellVal;
                                    if (k == taxCol)
                                    {
                                        taxSum += cellVal;
                                    }
                                }
                            }
                            if (str == "Pastries")
                            {
                                foodSum += workingSum;
                            }
                            else if (str.IndexOf("Merchandise") != -1 || str.IndexOf("Oz") != -1)
                            {
                                otherSum += workingSum;
                            }
                            else
                            {
                                preparedSum += workingSum;
                            }
                        }
                        if (str == "CATEGORY") // reached the header row, data starts on the next row
                        {
                            catFound = true;
                        }
                    }
                }
                workBook.Dispose();
                // Next read the modifiers file
                workBook = new XLWorkbook(GenericRoutines.registerFiles.Modifiers);
                workSheet = workBook.Worksheet(1);
                // grab the modifiers report header to verify that it's not the wrong report with the right name
                workCell = workSheet.Row(1).Cell(1);
                if (workCell is null || !workCell.Value.ToString().Contains("MODIFIER SALES REPORT"))
                {
                    GenericRoutines.UpdateAlerts(4, "FATAL ERROR", GenericRoutines.registerFiles.Modifiers.Substring(7) + " Is Not The Correct Report, IMPORT ABORTED");
                    return;
                }
                rowCount = workSheet.LastRowUsed()!.RowNumber();
                //read the worksheet rows to pull the modifiers sales
                bool modFound = false;
                for (int i = 1; i <= rowCount; i++) // 
                {
                    workCell = workSheet.Row(i).Cell(1);
                    if (workCell != null)
                    {
                        str = workCell.Value.ToString(); // grab modifier
                        if (modFound)
                        {
                            str = workSheet.Row(i).Cell(modsalesCol).Value.ToString(); // grab sales value as string
                            double.TryParse(str, out cellVal); // attempt conversion to double, ignore if false (cellVal will = 0)
                            preparedSum += cellVal;  // add modifier sales to prepared food total
                        }
                        if (str == "MODIFIER") // reached the header row, data starts on the next row
                        {
                            modFound = true;
                        }
                    }
                }
                workBook.Dispose();
                // Next read the payments file
                workBook = new XLWorkbook(GenericRoutines.registerFiles.Payments);
                workSheet = workBook.Worksheet(1);
                // grab the payments report header to verify that it's not the wrong report with the right name
                workCell = workSheet.Row(1).Cell(1);
                if (workCell is null || !workCell.Value.ToString().Contains("PAYMENT SUMMARY REPORT"))
                {
                    GenericRoutines.UpdateAlerts(4, "FATAL ERROR", GenericRoutines.registerFiles.Payments.Substring(7) + " Is Not The Correct Report, IMPORT ABORTED");
                    return;
                }
                rowCount = workSheet.LastRowUsed()!.RowNumber();
                //read the worksheet rows to get the payment lines
                for (int i = 1; i <= rowCount; i++)
                {
                    workCell = workSheet.Row(i).Cell(1);
                    if (workCell != null)
                    {
                        str = workCell.Value.ToString(); // grab payment type
                        if (str == "Credit Card" || str == "Cash")
                        {
                            double.TryParse(workSheet.Row(i).Cell(payCol).Value.ToString(), out cellVal); // attempt conversion to double, ignore if false (cellVal will = 0)
                            if (str == "Cash")
                            {
                                cashSum += cellVal;
                            }
                            else
                            {
                                ccSum += cellVal;
                            }
                        }
                    }
                }
                workBook.Dispose();
                sqlSupport.AddSQLParameter("@PreparedFood", SqlDbType.SmallMoney, preparedSum);
                sqlSupport.AddSQLParameter("@Merchandise", SqlDbType.SmallMoney, otherSum);
                sqlSupport.AddSQLParameter("@NonTaxableFood", SqlDbType.SmallMoney, foodSum);
                sqlSupport.AddSQLParameter("@TotalTaxCollected", SqlDbType.SmallMoney, taxSum);
                // add the parameters needed for the payments table
                sqlSupport.AddSQLParameter("@CoffeeCash", SqlDbType.SmallMoney, cashSum);
                sqlSupport.AddSQLParameter("@CoffeeCC", SqlDbType.SmallMoney, ccSum);
                // act on the transaction and payments tables
                _ = sqlSupport.ExecuteStoredProcedure(4);
            }
            catch (Exception ex)
            {
                GenericRoutines.UpdateAlerts(4, "FATAL ERROR",  ex.ToString() + ", IMPORT ABORTED");
                return;
            }
        }
        public void ReadKayakFiles()
        {
            // declare constants
            const int payCol = 5;
            int itemCol = 1, catCol = 1, salesCol = 1, taxCol = 1;
            int dataStart = 0;
            double boatSum = 0;
            double kayakSum = 0;
            double foodSum = 0;
            double otherSum = 0;
            double taxSum = 0;
            double ccSum = 0;
            double cashSum = 0;

            // Verify that all files exist.  If any are missing there is no point in processing further.
            if (!GenericRoutines.AllFilesPresent(5)) { return; }

            // Create the connection to the database and define the SQl command that calls the stored procedure.  Stop here it there's a problem
            SQLSupport sqlSupport = new SQLSupport(_dbConnectionService);
            if (!sqlSupport.PrepareForImport("UpdateKayakTable")) { return; }

            try
            {
                XLWorkbook workBook = new XLWorkbook(GenericRoutines.registerFiles.Sales);
                IXLWorksheet workSheet = workBook.Worksheet(1);
                IXLCell workCell;
                // grab the sales report header to verify that it's not the wrong report with the right name
                workCell = workSheet.Row(1).Cell(1);
                if (workCell is null || !workCell.Value.ToString().Contains("TOP ITEM SALES REPORT"))
                {
                    GenericRoutines.UpdateAlerts(5, "FATAL ERROR", GenericRoutines.registerFiles.Sales.Substring(7) + " Is Not The Correct Report, IMPORT ABORTED");
                    return;
                }
                int rowCount = workSheet.LastRowUsed()!.RowNumber();
                double cellVal, workingSum;
                string? str;
                string numStr;
                //read the lines unil we find the ITEMS header in column 1, then read the next line to identify the key columns
                for (int i = 1; i <= rowCount; i++) 
                {
                    if (workSheet.Row(i).Cell(1) != null && workSheet.Row(i).Cell(1).Value.ToString() == "ITEMS")
                    {
                        int colCount = workSheet.LastColumnUsed()!.ColumnNumber();
                        for (int j = 1; j <= colCount; j++)
                        {
                            str = workSheet.Row(i + 1).Cell(j).Value.ToString();
                            if (str == "ITEM")
                            {
                                itemCol = j;
                            }
                            else if (str == "CATEGORY")
                            {
                                catCol = j;
                            }
                            else if (str == "SALES")
                            {
                                salesCol = j;
                            }
                            else if (str == "TAX")
                            {
                                taxCol = j;
                            }
                        }
                        dataStart = i + 2;
                        break;
                    }
                }
                for (int i = dataStart; i <= rowCount; i++) // Stop 1 row short of the end, that's the Grand Total row
                {
                    workCell = workSheet.Row(i).Cell(itemCol);
                    str = workCell.Value.ToString(); // convert to string
                    workingSum = 0;
                    for (int k = salesCol; k <= taxCol; k++) // iterate over the colums to calculate total sales sum for the item
                    {
                        numStr = workSheet.Row(i).Cell(k).Value.ToString();
                        if (double.TryParse(numStr, out cellVal)) // attempt conversion to double, ignore if false (cellVal will = 0)
                        {
                            workingSum += cellVal;
                            if (k == taxCol)
                            {
                                taxSum += cellVal;
                            }
                        }
                    }
                    if (str.IndexOf("Pedal Boat") >= 0 || str.IndexOf("WEEKLY ALL BOATS") >= 0)
                    {
                        boatSum += workingSum;
                    }
                    else if (str.IndexOf("Kayak") >= 0 || str.IndexOf("Paddle") >= 0)
                    {
                        kayakSum += workingSum;
                    }
                    else if (str.IndexOf("Beverages") >= 0 || str.IndexOf("Dippin") >= 0 || workSheet.Row(i).Cell(catCol).Value.ToString() == "Kayak Ice Cream")
                    {
                        foodSum += workingSum;
                    }
                    else
                    {
                        otherSum += workingSum;
                    }
                }
                // Next read the payments file
                workBook = new XLWorkbook(GenericRoutines.registerFiles.Payments);
                workSheet = workBook.Worksheet(1);
                // grab the payments report header to verify that it's not the wrong report with the right name
                workCell = workSheet.Row(1).Cell(1);
                if (workCell is null || !workCell.Value.ToString().Contains("PAYMENT SUMMARY REPORT"))
                {
                    GenericRoutines.UpdateAlerts(5, "FATAL ERROR", GenericRoutines.registerFiles.Payments.Substring(7) + " Is Not The Correct Report, IMPORT ABORTED");
                    return;
                }
                rowCount = workSheet.LastRowUsed()!.RowNumber();
                string payStr;
                //read the worksheet rows to get the payment lines
                for (int i = 1; i <= rowCount; i++)
                {
                    workCell = workSheet.Row(i).Cell(1);
                    if (workCell != null)
                    {
                        str = workCell.Value.ToString(); // grab payment type
                        if (str == "Credit Card" || str == "Cash")
                        {
                            payStr = workSheet.Row(i).Cell(payCol).Value.ToString();
                            double.TryParse(payStr, out cellVal); // attempt conversion to double, ignore if false (cellVal will = 0)
                            if (str == "Cash")
                            {
                                cashSum += cellVal;
                            }
                            else
                            {
                                ccSum += cellVal;
                            }
                        }
                    }
                }
                sqlSupport.AddSQLParameter("@NonTaxableFood", SqlDbType.SmallMoney, foodSum);
                sqlSupport.AddSQLParameter("@KayaksandBoards", SqlDbType.SmallMoney, kayakSum);
                sqlSupport.AddSQLParameter("@PedalBoats", SqlDbType.SmallMoney, boatSum);
                sqlSupport.AddSQLParameter("@MiscSales", SqlDbType.SmallMoney, otherSum);
                sqlSupport.AddSQLParameter("@TotalTaxCollected", SqlDbType.SmallMoney, taxSum);
                // add the parameters needed for the payments table
                sqlSupport.AddSQLParameter("@KayakCC", SqlDbType.SmallMoney, ccSum);
                sqlSupport.AddSQLParameter("@KayakCash", SqlDbType.SmallMoney, cashSum);
                // act on the transaction table
                _ = sqlSupport.ExecuteStoredProcedure(5);
            }
            catch (Exception ex)
            {
                GenericRoutines.UpdateAlerts(5, "FATAL ERROR",  ex.ToString() + ", IMPORT ABORTED");
                return;
            }
        }
        public void ReadGuestFiles()
        {
            // declare constants
            double netSum = 0;
            double ccSum = 0;
            double cashSum = 0;

            // Verify that all files exist.  If any are missing there is no point in processing further.
            if (!GenericRoutines.AllFilesPresent(9)) { return; }

            // Create the connection to the database and define the SQl command that calls the stored procedure.  Stop here it there's a problem
            SQLSupport sqlSupport = new SQLSupport(_dbConnectionService);
            if (!sqlSupport.PrepareForImport("UpdateMiscTable")) { return; }

            try
            {
                Spire.Xls.Workbook spireWB = new Spire.Xls.Workbook();
                spireWB.LoadFromFile(GenericRoutines.singleFile.Data, ",", 1, 1);
                Spire.Xls.Worksheet workSheet = spireWB.Worksheets[0];
                int rowCount = workSheet.LastDataRow;
                string str;
                string numStr;
                //iterate over the worksheet rows until we get the values we need
                for (int i = 0; i < rowCount; i++)
                {
                    IXLSRange[] columns = workSheet.Rows[i].Columns;
                    if (i == 0 && columns[0].Value != "Sales Overview Report")
                    {
                        GenericRoutines.UpdateAlerts(9, "FATAL ERROR", "Incorrect Sales Overview Report format");
                        return;
                    }
                    foreach (IXLSRange column in columns)
                    {
                        if (columns[0].Value == null) { break; }
                        str = columns[0].Value;
                        if (str == "Net Sales" || str == "Cash" || str == "Visa" || str == "MasterCard" || str == "Discover" || str == "American Express") // this is an item row to be processed
                        {
                            if (str == "Net Sales")
                            {
                                numStr = columns[1].Value.Replace("$", ""); // remove $ sign or TryParse will fail
                            }
                            else
                            {
                                numStr = columns[3].Value.Replace("$", ""); // remove $ sign or TryParse will fail
                            }
                            if (double.TryParse(numStr, out double cellVal)) // attempt conversion to double, ignore if false (cellVal will = 0)
                            {
                                if (str == "Net Sales") { netSum = cellVal; break; }
                                else if (str == "Cash") { cashSum = cellVal; break; }
                                else if (str == "Visa") { ccSum += cellVal; break; }
                                else if (str == "MasterCard") { ccSum += cellVal; break; }
                                else if (str == "Discover") { ccSum += cellVal; break; }
                                else if (str == "American Express") { ccSum += cellVal; break; }
                                else { }
                            }
                        }
                        else
                        {
                            break;  // we don't need this row
                        }
                    }
                }
                sqlSupport.AddSQLParameter("@GuestServices", SqlDbType.SmallMoney, netSum);
                sqlSupport.AddSQLParameter("@GuestCC", SqlDbType.SmallMoney, ccSum);
                sqlSupport.AddSQLParameter("@GuestCash", SqlDbType.SmallMoney, cashSum);
                // act on the transaction and payments table
                _ = sqlSupport.ExecuteStoredProcedure(9);
            }
            catch (Exception ex)
            {
                GenericRoutines.UpdateAlerts(9, "FATAL ERROR",  ex.ToString() + ", IMPORT ABORTED");
                return;
            }
        }
        public void ReadSpecialAddonsFile()
        {
            // Verify that the file exists.  If not there is no point in processing further.
            if (!GenericRoutines.AllFilesPresent(6)) { return; }

            // Create the connection to the database and define the SQl command that calls the stored procedure.  Stop here it there's a problem
            SQLSupport sqlSupport = new SQLSupport(_dbConnectionService);
            if (!sqlSupport.PrepareForImport("UpdateMiscTable")) { return; }

            // define the category, tax rate multiplier, and sales accumulator arrays
            string[] glArray = { "0326", "0328", "0348", "0352", "0359", "0384", "0392", "0393", "0925" };
            string[] paramArray = {"@Vending", "@Laundry", "@ArcadeGames", "@LeasedConcessions", "@Activities", "@CATV", "@GuestServices",
                                    "@Other", "@Misc"};
            double[] glSum = new double[glArray.Length];    // double type defaults to 0.0 so don't need to initialize

            try
            {
                XLWorkbook workBook = new XLWorkbook(GenericRoutines.singleFile.Data);
                IXLWorksheet workSheet = workBook.Worksheet(1);
                IXLCell workCell;
                int rowCount = workSheet.LastRowUsed()!.RowNumber();

                string str, miscDesc = "";
                string repDateShort = GenericRoutines.repDateTmp.ToShortDateString();
                //iterate over the worksheet rows to accumulate the sales data by category
                for (int i = 2; i <= rowCount; i++) // skip the header row
                {
                    workCell = workSheet.Row(i).Cell(1);
                    if (workCell != null)
                    {
                        System.DateTime tmpDate = workCell.Value; // get date from spreadsheet
                        string tmpDateStr = tmpDate.ToShortDateString();
                        if (tmpDateStr == repDateShort)
                        {
                            string glStr = "0" + workSheet.Row(i).Cell(2).Value.ToString();
                            int j = 0;
                            do
                            {
                                if (glArray[j] == glStr)
                                {
                                    str = workSheet.Row(i).Cell(4).Value.ToString(); // grab amount as string
                                    if (double.TryParse(str, out double cellVal)) // attempt conversion to double, ignore if false (cellVal will = 0)
                                    {
                                        glSum[j] += cellVal;
                                        if (glStr == "0925")
                                        {
                                            miscDesc += workSheet.Row(i).Cell(3).Value.ToString();
                                        }
                                        break;
                                    }
                                }
                                j += 1; // iterate to entry in the array
                            }
                            while (j < glArray.Length);
                            if (j == glArray.Length)
                            {
                                GenericRoutines.UpdateAlerts(6, "FATAL ERROR", "Unknown GL code " + glStr + ", IMPORT ABORTED");
                                return;
                            }
                        }
                    }
                }
                // loop through the glsum array to add the parameters and their values needed for any non-zero sums for stored procedure
                for (int i = 0; i <= glSum.GetUpperBound(0); i++)
                {
                    if (glSum[i] != 0)
                    {
                        sqlSupport.AddSQLParameter((string)paramArray[i], SqlDbType.SmallMoney, glSum[i]);
                        if (paramArray[i] == "@Misc")
                        {
                            sqlSupport.AddSQLParameterString((string)"@MiscDesc", SqlDbType.NChar, miscDesc);
                        }
                    }
                }
                // act on the transaction table
                _ = sqlSupport.ExecuteStoredProcedure(6);
            }
            catch (Exception ex)
            {
                GenericRoutines.UpdateAlerts(6, "FATAL ERROR",  ex.ToString() + ", IMPORT ABORTED");
                return;
            }
        }
    } // END OF CLASS
} // END OF NAMESPACE
