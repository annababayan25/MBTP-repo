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
        public static void UpdateAlerts(byte pcidIn, string severityIn, string textIn)
        {
            var connStr = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("ConnectionStrings")["DefaultConnection"];
            SqlConnection alertSqlConn = new SqlConnection(connStr);
            SqlCommand alterCmd = new SqlCommand("dbo.UpdateAlerts", alertSqlConn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            // add input parameter for transaction date
            alterCmd.Parameters.Add("@TransDate", SqlDbType.Date).Value = GenericRoutines.repDateStr;
            alterCmd.Parameters.Add("@PCID", SqlDbType.TinyInt).Value = pcidIn;
            alterCmd.Parameters.Add("@Severity", SqlDbType.VarChar, 50).Value = severityIn;
            alterCmd.Parameters.Add("@AlertText", SqlDbType.VarChar, 4000).Value = textIn;
            // add an output parameter to check if stored procedures executed cleanly
            alterCmd.Parameters.Add("@status", SqlDbType.NVarChar, 4000);
            alterCmd.Parameters["@status"].Direction = ParameterDirection.Output;
            alertSqlConn.Open();
            alterCmd.ExecuteNonQuery();
            alertSqlConn.Close();
        }
        public static string DoesFileExist(string subDirectoryIn, string fileNameIn, string suffixIn, bool modifyCheck = false)
        {
            string repDate = repDateTmp.ToString("MMMdd").ToUpper();
            //DateTime tmpParseDate = DateTime.Today;
            //if (dateToUse != null)
            //{
            //    tmpParseDate = DateTime.Parse(dateToUse);
            //    repDate = tmpParseDate.ToString("MMMdd").ToUpper();
            //}
            // although counterintuitive, look to see if the files exist in an archive directory first.  We need to do this so that it won't
            // grab the wrong October files.  This forces it to look for the previous fiscal year files and not grab the current October files
            // if any historical reporting needs to be done during year-end closeout.
            string workingFilePath;
            if(repDateTmp.Month >= 10)
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
                if (modifyCheck) // this only applies to Newbook files.  If a modified version is found that path is returned
                {
                    string modifiedFilePath = workingFilePath.Replace(suffixIn," - MODIFIED" + suffixIn);
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
            else // now we look for the file in the standard downloads directory
            {
                //workingFilePath = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("ExportRetrieval")["Path"];
                //if (subDirectoryIn != ""){
                //    workingFilePath += "%2F" + subDirectoryIn;
                //}
                //workingFilePath += "%2F" + fileNameIn + repDate + suffixIn;
                workingFilePath = dirPath + subDirectoryIn + fileNameIn + repDate + suffixIn;
                if (System.IO.File.Exists(workingFilePath))
                {
                    if (modifyCheck) // this only applies to Newbook files.  If a modified version is found that path is returned
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
            // although counterintuitive, look to see if the files exist in an archive directory first.  We need to do this so that it won't
            // grab the wrong October files.  This forces it to look for the previous fiscal year files and not grab the current October files
            // if any historical reporting needs to be done during year-end closeout.
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
                // check for a modified version first
                string modifiedFilePath = workingFilePath.Replace(suffixIn, " - MODIFIED" + suffixIn);
                if (System.IO.File.Exists(modifiedFilePath))
                {
                    workingFilePath = modifiedFilePath;
                }
            }
            else // now we look for the file in the standard downloads directory
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
                if (checkedInSheet.Row(listCounter).Cell(3).Value.ToString().Length == 6 && checkedInSheet.Row(listCounter).Cell(3).Value.ToString().Substring(0, 6) == bookingIDIn)
                {
                    checkedInBook.Dispose();
                    return true;
                }
            }
        return false;
        }
        public static bool BlackedOutDate(string dateIn, byte pcidIn)
        {
//            SqlConnection sqlConn = new System.Data.SqlClient.SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);
            var connStr = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("ConnectionStrings")["DefaultConnection"];
            SqlConnection sqlConn = new SqlConnection(connStr);
            SqlCommand cmd = new SqlCommand("dbo.F_BlackoutDate", sqlConn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            // add input parameter for transaction date
            cmd.Parameters.Add("@TransDate", SqlDbType.Date);
            cmd.Parameters["@TransDate"].Value = dateIn;
            cmd.Parameters.Add("@Result", SqlDbType.Bit);
            cmd.Parameters["@Result"].Direction = ParameterDirection.ReturnValue;
            sqlConn.Open();
            cmd.ExecuteScalar();
            sqlConn.Close();

            return (bool)cmd.Parameters["@Result"].Value;
        }
        public static bool AllFilesPresent(int pcIDIn)
        {
            string subDirName;
            bool failureEncountered = true;
            switch (pcIDIn)
            {
                case var i when pcIDIn == 1:
                    {
                        subDirName = "";
                        break;
                    }
                case var i when pcIDIn == 2 || (pcIDIn >= 4 && pcIDIn < 6) || pcIDIn == 9:
                    {
                        subDirName = @"Store Exports\";
                        break;
                    }
                case var i when pcIDIn == 3:
                    {
                        subDirName = @"Arcade Exports\";
                        break;
                    }
                default:
                    {
                        subDirName = "";
                        break;
                    }
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
                                GenericRoutines.UpdateAlerts(1, "FATAL ERROR", fileCheck.Key + ".xlsx" + " Not Found, NEWBOOK IMPORT ABORTED");
                                return false;
                            }
                        }
                        return true;
                        //return !filesToCheck.Values.Any(path => path == null); // logically negate the path check so the result of method will be true if all files were located
                   }
                case 2: // Store
                    {
                        // look for sales file
                        string path__1 = GenericRoutines.DoesFileExist(subDirName, @"Store Sales ", ".xlsx");
                        if (path__1.IndexOf("FAILURE") == -1)   // The file was found, look for tax file
                        {
                            storeFiles.Sales = path__1;
                            path__1 = GenericRoutines.DoesFileExist(subDirName, @"Store Tax ", ".xlsx");
                            if (path__1.IndexOf("FAILURE") == -1)   // The file was found, look for payments file
                            {
                                storeFiles.Tax = path__1;
                                path__1 = GenericRoutines.DoesFileExist(subDirName, @"Store CC ", ".xlsx");
                                if (path__1.IndexOf("FAILURE") == -1)   // The file was found, set boolean to false
                                {
                                    storeFiles.Payments = path__1;
                                    failureEncountered = false;
                                }
                            }
                        }
                        else // check for blacked out date if sales file not found.  if blacked out this is not an error condition
                        {
                            if (BlackedOutDate(GenericRoutines.repDateStr, 2))
                            {
                                GenericRoutines.UpdateAlerts(2, "SUCCESS", ""); // Can't be alerts if it wasn't operating
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
                        // look for sales file
                        string path__1 = GenericRoutines.DoesFileExist(subDirName, @"Arcade Sales ", ".xlsx");
                        if (path__1.IndexOf("FAILURE") == -1)   // The file was found, look for payments file
                        {
                            registerFiles.Sales = path__1;
                            path__1 = GenericRoutines.DoesFileExist(subDirName, @"Arcade Payments ", ".xlsx");
                            if (path__1.IndexOf("FAILURE") == -1)   // The file was found, set boolean to false
                            {
                                registerFiles.Payments = path__1;
                                failureEncountered = false;
                            }
                        }
                        else // check for blacked out date if sales file not found.  if blacked out this is not an error condition
                        {
                            if (BlackedOutDate(GenericRoutines.repDateStr, 3))
                            {
                                GenericRoutines.UpdateAlerts(3, "SUCCESS", ""); // Can't be alerts if it wasn't operating
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
                        // look for sales file
                        string path__1 = GenericRoutines.DoesFileExist(subDirName, @"Coffee Item Sales ", ".xlsx");
                        if (path__1.IndexOf("FAILURE") == -1)   // The file was found, look for tax file
                        {
                            registerFiles.Sales = path__1;
                            path__1 = GenericRoutines.DoesFileExist(subDirName, @"Coffee Modifier Sales ", ".xlsx");
                            if (path__1.IndexOf("FAILURE") == -1)   // The file was found, look for payments file
                            {
                                registerFiles.Modifiers = path__1;
                                path__1 = GenericRoutines.DoesFileExist(subDirName, @"Coffee Payments ", ".xlsx");
                                if (path__1.IndexOf("FAILURE") == -1)   // The file was found, set boolean to false
                                {
                                    registerFiles.Payments = path__1;
                                    failureEncountered = false;
                                }
                            }
                        }
                        else // check for blacked out date if sales file not found.  if blacked out this is not an error condition
                        {
                            if (BlackedOutDate(GenericRoutines.repDateStr, 4))
                            {
                                GenericRoutines.UpdateAlerts(4, "SUCCESS", ""); // Can't be alerts if it wasn't operating
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
                        // look for sales file
                        string path__1 = GenericRoutines.DoesFileExist(subDirName, @"Kayak Item Sales ", ".xlsx");
                        if (path__1.IndexOf("FAILURE") == -1)   // The file was found, look for tax file
                        {
                            registerFiles.Sales = path__1;
                            path__1 = GenericRoutines.DoesFileExist(subDirName, @"Kayak Payments ", ".xlsx");
                            if (path__1.IndexOf("FAILURE") == -1)   // The file was found, set boolean to false
                            {
                                registerFiles.Payments = path__1;
                                failureEncountered = false;
                            }
                        }
                        else // check for blacked out date if sales file not found.  if blacked out this is not an error condition
                        {
                            if (BlackedOutDate(GenericRoutines.repDateStr, 5))
                            {
                                GenericRoutines.UpdateAlerts(5, "SUCCESS", ""); // Can't be alerts if it wasn't operating
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
                        // the file should be in the standard downloads directory
                        // We do not call DoesFileExist since the location and name of this file are fixed.
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
                        // look for the single data file
                        string path__1 = GenericRoutines.DoesFileExist(subDirName, @"Guest Services - ", ".csv");
                        if (path__1.IndexOf("FAILURE") == -1)   // The file was found, set boolean to false
                        {
                            singleFile.Data = path__1;
                            failureEncountered = false;
                        }
                        else // check for blacked out date if sales file not found.  if blacked out this is not an error condition
                        {
                            if (BlackedOutDate(GenericRoutines.repDateStr, 9))
                            {
                                GenericRoutines.UpdateAlerts(9, "SUCCESS", ""); // Can't be alerts if it wasn't operating
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
                    {
                        break;
                    }
            }
            return !failureEncountered; // logically negate the variable (the result of method will be true if all files were located)
        }
    }
}
