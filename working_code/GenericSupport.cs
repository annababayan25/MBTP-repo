using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Web;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using SQLStuff;
using Microsoft.EntityFrameworkCore.Storage;
using MBTP.Interfaces;

namespace GenericSupport
{
    public class NewbookFiles
    {
        #nullable enable
        public string? Transflow { get; set; }
        public string? Recon { get; set; }
        public string? Invitems { get; set; }
        public string? DepcurrQ { get; set; }
        public string? DepprevQ { get; set; }
        public string? DepprevQ2 { get; set; }
        public string? Bookadj { get; set; }
        public string? Checkedin { get; set; }
        public string? Bookchart { get; set; }
        public string? Bookdep { get; set; }
        public string? Bookstay { get; set; }
        public string? Occupancy { get; set; }
    }

    public class HeartlandRetailFiles
    {
        public string? Sales { get; set; }
        public string? Tax { get; set; }
        public string? Payments { get; set; }
    }

    #nullable disable
    public class HeartlandRegisterFiles
    {
        public string Sales { get; set; }
        public string Modifiers { get; set; }
        public string Payments { get; set; }
    }

    public class SingleFilesOperation
    {
        public string Data { get; set; }
    }

    public class GenericRoutines
    {
        public const string dirPath = @"\\DAVE-800-G3\Users\dgriffis.MBTP\OneDrive - MYRTLE BEACH TRAVEL PARK\Downloads\";
        public const string altPath = @"\\DAVE-800-G3\Users\dgriffis.MBTP\OneDrive - MYRTLE BEACH TRAVEL PARK\Daily Export Files Archives\";
        public static string repDateStr;
        public static System.DateTime repDateTmp;
        public static HeartlandRetailFiles storeFiles = new HeartlandRetailFiles();
        public static HeartlandRegisterFiles registerFiles = new HeartlandRegisterFiles();
        public static SingleFilesOperation singleFile = new SingleFilesOperation();
        public static NewbookFiles nbfiles = new NewbookFiles();
        public static IDatabaseConnectionService _dbConnectionService;

        public static void Initialize(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
        public static void UpdateAlerts(byte pcidIn, string severityIn, string textIn)
        {
            SQLSupport.UpdateAlertsTable(_dbConnectionService, pcidIn, severityIn, textIn);
        }

        public static string DoesFileExist(string subDirectoryIn, string fileNameIn, string suffixIn, bool modifyCheck = false)
        {
            string repDate = repDateTmp.ToString("MMMdd").ToUpper();
            string workingFilePath;

            if (repDateTmp.Month >= 10)
            {
                workingFilePath = altPath + "FY" + repDateTmp.ToString("yyyy") + @"\" + repDateTmp.ToString("MMM") + @"\";
            }
            else
            {
                workingFilePath = altPath + "FY" + (repDateTmp.Year - 1).ToString() + @"\" + repDateTmp.ToString("MMM") + @"\";
            }

            workingFilePath = workingFilePath + subDirectoryIn + fileNameIn + repDate + suffixIn;

            if (System.IO.File.Exists(workingFilePath))
            {
                if (modifyCheck)
                {
                    string modifiedFilePath = workingFilePath.Replace(suffixIn, " - MODIFIED" + suffixIn);
                    if (System.IO.File.Exists(modifiedFilePath))
                    {
                        return modifiedFilePath;
                    }
                    else
                    {
                        return workingFilePath;
                    }
                }
                return workingFilePath;
            }
            else
            {
                workingFilePath = dirPath + subDirectoryIn + fileNameIn + repDate + suffixIn;
                if (System.IO.File.Exists(workingFilePath))
                {
                    if (modifyCheck)
                    {
                        string modifiedFilePath = workingFilePath.Replace(suffixIn, " - MODIFIED" + suffixIn);
                        if (System.IO.File.Exists(modifiedFilePath))
                        {
                            return modifiedFilePath;
                        }
                        else
                        {
                            return workingFilePath;
                        }
                    }
                    return workingFilePath;
                }
                else
                {
                    return "FAILURE" + fileNameIn + repDate + suffixIn;
                }
            }
        }

        public static bool DidGuestArrive(string subDirectoryIn, string fileNameIn, string suffixIn, DateTime arrDateIn, string bookingIDIn)
        {
            string arrDateToCheck = arrDateIn.ToString("MMMdd").ToUpper();
            string workingFilePath;

            if (arrDateIn.Month >= 10)
            {
                workingFilePath = altPath + "FY" + arrDateIn.ToString("yyyy") + @"\" + arrDateIn.ToString("MMM") + @"\";
            }
            else
            {
                workingFilePath = altPath + "FY" + (arrDateIn.Year - 1).ToString() + @"\" + arrDateIn.ToString("MMM") + @"\";
            }

            workingFilePath = workingFilePath + subDirectoryIn + fileNameIn + arrDateToCheck + suffixIn;

            if (System.IO.File.Exists(workingFilePath))
            {
                string modifiedFilePath = workingFilePath.Replace(suffixIn, " - MODIFIED" + suffixIn);
                if (System.IO.File.Exists(modifiedFilePath))
                {
                    workingFilePath = modifiedFilePath;
                }
            }
            else
            {
                workingFilePath = dirPath + subDirectoryIn + fileNameIn + arrDateToCheck + suffixIn;
                if (System.IO.File.Exists(workingFilePath))
                {
                    string modifiedFilePath = workingFilePath.Replace(suffixIn, " - MODIFIED" + suffixIn);
                    if (System.IO.File.Exists(modifiedFilePath))
                    {
                        workingFilePath = modifiedFilePath;
                    }
                }
                else
                {
                    return false;
                }
            }

            XLWorkbook checkedInBook = new XLWorkbook(workingFilePath);
            IXLWorksheet checkedInSheet = checkedInBook.Worksheet(1);
            int listRowCount = checkedInSheet.LastRowUsed().RowNumber();

            for (int listCounter = 2; listCounter <= listRowCount; listCounter++)
            {
                if (checkedInSheet.Row(listCounter).Cell(3).Value.ToString().Length == 6 &&
                    checkedInSheet.Row(listCounter).Cell(3).Value.ToString().Substring(0, 6) == bookingIDIn)
                {
                    checkedInBook.Dispose();
                    return true;
                }
            }
            return false;
        }

        public static bool IsOperationBlackedOut(DateTime dateToCheck, byte pcidIn, out string reason)
        {
            reason = string.Empty;
            using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
            using (SqlCommand cmd = new SqlCommand("dbo.RetrieveBlackoutState", sqlConn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@StartDate", SqlDbType.Date).Value = dateToCheck;
                cmd.Parameters.Add("@EndDate", SqlDbType.Date).Value = dateToCheck;
                sqlConn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        switch (pcidIn)
                        {
                            case 2:
                                if (Convert.ToInt32(reader["StoreClosedState"]) == 1)
                                {
                                    reason = reader["StoreClosedReason"]?.ToString();
                                    return true;
                                }
                                break;
                            case 3:
                                if (Convert.ToInt32(reader["ArcadeClosedState"]) == 1)
                                {
                                    reason = reader["ArcadeClosedReason"]?.ToString();
                                    return true;
                                }
                                break;
                            case 4:
                                if (Convert.ToInt32(reader["CoffeeClosedState"]) == 1)
                                {
                                    reason = reader["CoffeeClosedReason"]?.ToString();
                                    return true;
                                }
                                break;
                            case 5:
                                if (Convert.ToInt32(reader["KayakClosedState"]) == 1)
                                {
                                    reason = reader["KayakClosedReason"]?.ToString();
                                    return true;
                                }
                                break;
                            case 9:
                                if (Convert.ToInt32(reader["Blackout"]) == 1)
                                {
                                    reason = "Guest Services blackout";
                                    return true;
                                }
                                break;
                        }
                    }
                }
            }
            return false;
        }

        public static bool AllFilesPresent(int pcIDIn)
        {
            string subDirName;
            bool failureEncountered = true;

            switch (pcIDIn)
            {
                case var i when pcIDIn == 1:
                    subDirName = "";
                    break;
                case var i when pcIDIn == 2 || (pcIDIn >= 4 && pcIDIn < 6) || pcIDIn == 9:
                    subDirName = @"Store Exports\";
                    break;
                case var i when pcIDIn == 3:
                    subDirName = @"Arcade Exports\";
                    break;
                default:
                    subDirName = "";
                    break;
            }

            switch (pcIDIn)
            {
                case 1: // Newbook
                    {
                        var filesToCheck = new Dictionary<string, Action<string>>()
                        {
                            { "Transaction_Flow_", path => nbfiles.Transflow = path },
                            { "Reconciliation_Report_", path => nbfiles.Recon = path },
                            { "Inventory_Items_", path => nbfiles.Invitems = path },
                            { "Bookings_Departing_List_Current_Quarter_", path => nbfiles.DepcurrQ = path },
                            { "Bookings_Departing_List_Previous_Quarter_", path => nbfiles.DepprevQ = path },
                            { "Bookings_Departing_List_2nd_Previous_Quarter_", path => nbfiles.DepprevQ2 = path },
                            { "Booking_Adjustments_", path => nbfiles.Bookadj = path },
                            { "Checked_In_List_", path => nbfiles.Checkedin = path },
                            { "Bookings_Chart_", path => nbfiles.Bookchart = path },
                            { "Bookings_Departing_", path => nbfiles.Bookdep = path },
                            { "Bookings_Staying_", path => nbfiles.Bookstay = path },
                            { "Occupancy_Report_", path => nbfiles.Occupancy = path }
                        };

                        foreach (var fileCheck in filesToCheck)
                        {
                            string path = GenericRoutines.DoesFileExist(subDirName, fileCheck.Key, ".xlsx", true);
                            if (path.IndexOf("FAILURE") == -1)
                            {
                                fileCheck.Value(path);
                            }
                            else
                            {
                                GenericRoutines.UpdateAlerts(1, "FATAL ERROR", fileCheck.Key + repDateTmp.ToString("MMMdd").ToUpper() + ".xlsx Not Found, NEWBOOK IMPORT ABORTED");
                                return false;
                            }
                        }
                        return true;
                    }

                case 2: // Store
                    {
                        string path__1 = GenericRoutines.DoesFileExist(subDirName, @"Store Sales ", ".xlsx");
                        if (path__1.IndexOf("FAILURE") == -1)
                        {
                            storeFiles.Sales = path__1;
                            path__1 = GenericRoutines.DoesFileExist(subDirName, @"Store Tax ", ".xlsx");
                            if (path__1.IndexOf("FAILURE") == -1)
                            {
                                storeFiles.Tax = path__1;
                                path__1 = GenericRoutines.DoesFileExist(subDirName, @"Store CC ", ".xlsx");
                                if (path__1.IndexOf("FAILURE") == -1)
                                {
                                    storeFiles.Payments = path__1;
                                    failureEncountered = false;
                                }
                            }
                        }
                        else
                        {
                            if (IsOperationBlackedOut(GenericRoutines.repDateTmp, 2, out string reason))
                            {
                                GenericRoutines.UpdateAlerts(2, "SUCCESS", $"Skipped due to blackout: {reason}");
                            }
                            else
                            {
                                GenericRoutines.UpdateAlerts(2, "FATAL ERROR", path__1.Substring(7) + " Not Found, GENERAL STORE IMPORT ABORTED");
                            }
                            return false;
                        }
                        break;
                    }

                case 3: // Arcade
                    {
                        string path__1 = GenericRoutines.DoesFileExist(subDirName, @"Arcade Sales ", ".xlsx");
                        if (path__1.IndexOf("FAILURE") == -1)
                        {
                            registerFiles.Sales = path__1;
                            path__1 = GenericRoutines.DoesFileExist(subDirName, @"Arcade Payments ", ".xlsx");
                            if (path__1.IndexOf("FAILURE") == -1)
                            {
                                registerFiles.Payments = path__1;
                                failureEncountered = false;
                            }
                        }
                        else
                        {
                            if (IsOperationBlackedOut(GenericRoutines.repDateTmp, 3, out string reason))
                            {
                                GenericRoutines.UpdateAlerts(3, "SUCCESS", $"Skipped due to blackout: {reason}");
                            }
                            else
                            {
                                GenericRoutines.UpdateAlerts(3, "FATAL ERROR", path__1.Substring(7) + " Not Found, ARCADE IMPORT ABORTED");
                            }
                            return false;
                        }
                        break;
                    }

                case 4: // Coffee Trailer
                    {
                        string path__1 = GenericRoutines.DoesFileExist(subDirName, @"Coffee Item Sales ", ".xlsx");
                        if (path__1.IndexOf("FAILURE") == -1)
                        {
                            registerFiles.Sales = path__1;
                            path__1 = GenericRoutines.DoesFileExist(subDirName, @"Coffee Modifier Sales ", ".xlsx");
                            if (path__1.IndexOf("FAILURE") == -1)
                            {
                                registerFiles.Modifiers = path__1;
                                path__1 = GenericRoutines.DoesFileExist(subDirName, @"Coffee Payments ", ".xlsx");
                                if (path__1.IndexOf("FAILURE") == -1)
                                {
                                    registerFiles.Payments = path__1;
                                    failureEncountered = false;
                                }
                            }
                        }
                        else
                        {
                            if (IsOperationBlackedOut(GenericRoutines.repDateTmp, 4, out string reason))
                            {
                                GenericRoutines.UpdateAlerts(4, "SUCCESS", $"Skipped due to blackout: {reason}");
                            }
                            else
                            {
                                GenericRoutines.UpdateAlerts(4, "FATAL ERROR", path__1.Substring(7) + " Not Found, COFFEE TRAILER IMPORT ABORTED");
                            }
                            return false;
                        }
                        break;
                    }

                case 5: // Kayak Shack
                    {
                        string path__1 = GenericRoutines.DoesFileExist(subDirName, @"Kayak Item Sales ", ".xlsx");
                        if (path__1.IndexOf("FAILURE") == -1)
                        {
                            registerFiles.Sales = path__1;
                            path__1 = GenericRoutines.DoesFileExist(subDirName, @"Kayak Payments ", ".xlsx");
                            if (path__1.IndexOf("FAILURE") == -1)
                            {
                                registerFiles.Payments = path__1;
                                failureEncountered = false;
                            }
                        }
                        else
                        {
                            if (IsOperationBlackedOut(GenericRoutines.repDateTmp, 5, out string reason))
                            {
                                GenericRoutines.UpdateAlerts(5, "SUCCESS", $"Skipped due to blackout: {reason}");
                            }
                            else
                            {
                                GenericRoutines.UpdateAlerts(5, "FATAL ERROR", path__1.Substring(7) + " Not Found, KAYAK SHACK IMPORT ABORTED");
                            }
                            return false;
                        }
                        break;
                    }

                case 6: // Special Addons Spreadsheet
                    {
                        string workingFilePath = dirPath + @"Special Daily Income Addons.xlsx";
                        if (System.IO.File.Exists(workingFilePath))
                        {
                            singleFile.Data = workingFilePath;
                            failureEncountered = false;
                        }
                        else
                        {
                            GenericRoutines.UpdateAlerts(6, "FATAL ERROR", workingFilePath + " Not Found, IMPORT ABORTED");
                        }
                        break;
                    }

                case 9: // Guest Services
                    {
                        string path__1 = GenericRoutines.DoesFileExist(subDirName, @"Guest Services - ", ".csv");
                        if (path__1.IndexOf("FAILURE") == -1)
                        {
                            singleFile.Data = path__1;
                            failureEncountered = false;
                        }
                        else
                        {
                            if (IsOperationBlackedOut(GenericRoutines.repDateTmp, 9, out string reason))
                            {
                                GenericRoutines.UpdateAlerts(9, "SUCCESS", $"Skipped due to blackout: {reason}");
                            }
                            else
                            {
                                GenericRoutines.UpdateAlerts(9, "FATAL ERROR", path__1.Substring(7) + " Not Found, GUEST SERVICES IMPORT ABORTED");
                            }
                            return false;
                        }
                        break;
                    }

                default:
                    break;
            }
            return !failureEncountered;
        }
    }
}

