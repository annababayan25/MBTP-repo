using ClosedXML.Excel;
using GenericSupport;
using MBTP.Logins;
using NewbookSupport;
using SQLStuff;
using System;
using System.Data;

namespace FinancialC_
{
    public class NewbookImport
    {
        public static void ReadNewbookFiles()
        {
            string tmpAction, tmpDesc, tmpCat, tmpTrans, tmpID, tmpClient, tmpGen, flowStr;
            double tmpVal, totAmex = 0, totOtherCC = 0, totCash = 0, lockFee, siteDeposit = 100, rentalDeposit = 200, vehicleRateDayTax = 5.6;
            System.DateTime arrDate, departDate;
            int startRow;
            bool refundChecksActive = false;
            const double visitorRateBase = 4;
            const double visitorRateTax = 4.2;
            const double vehicleRateDayBase = 5;
            const double vehicleRateYrBase = 40;
            const double vehicleRateYrTax = 42;
            const double wristbandRate = 5;
            const int catCol = 2;
            const int transCol = 3;
            const int clientCol = 5;
            const int genCol = 6;
            const int descCol = 7;
            const int amtCol = 8;
            const int arrCol = 9;
            const int depCol = 10;

            // Verify that all files exist.  If any are missing there is no point in processing further.
            if (!GenericRoutines.AllFilesPresent(1)) 
            {
                return; 
            }

            // Create the connection to the database and define the SQl command that calls the stored procedure.  Stop here it there's a problem
            if (!SQLSupport.PrepareForImport("UpdateFrontOfficeTable")) 
            { 
                return;
            }

            if (System.DateTime.Parse(GenericRoutines.repDateStr) < System.DateTime.Parse("2024-01-01"))
            {
                lockFee = 30;
            }
            else
            {
                lockFee = 40;
            }
            // initialize revenue array
            var revenueArray = new List<NewbookSupport.Revenue>
            {
                new Revenue() { RevType = "Annual", Accum = 0 },
                new Revenue() { RevType = "Employee", Accum = 0 },
                new Revenue() { RevType = "LTSites", Accum = 0 },
                new Revenue() { RevType = "LTUnits", Accum = 0 },
                new Revenue() { RevType = "Campsites", Accum = 0 },
                new Revenue() { RevType = "MHPark", Accum = 0 },
                new Revenue() { RevType = "Rentals", Accum = 0 },
                new Revenue() { RevType = "LockFees", Accum = 0 },
                new Revenue() { RevType = "Storage", Accum = 0 },
                new Revenue() { RevType = "Misc", Accum = 0 },
                new Revenue() { RevType = "LateFees", Accum = 0 },
                new Revenue() { RevType = "DamageFees", Accum = 0 },
                new Revenue() { RevType = "GolfCartRentals", Accum = 0 }
            };
            // initialize deposits arrays
            int tmpMonth = int.Parse(GenericRoutines.repDateStr.Substring(5, 2));
            int tmpYear = int.Parse(GenericRoutines.repDateStr.Substring(0, 4)) - (tmpMonth < 10 ? 1 : 0); // Fiscal year starts with month 10
            var depositsArray = new List<NewbookSupport.Deposits>
            {
                new Deposits() { Fy = System.DateTime.Parse("10/01/"+(tmpYear+2).ToString()), WescAccum = 0, RentalAccum = 0, GolfAccum = 0, VouchersAccum = 0 },
                new Deposits() { Fy = System.DateTime.Parse("10/01/"+(tmpYear+1).ToString()), WescAccum = 0, RentalAccum = 0, GolfAccum = 0, VouchersAccum = 0 },
                new Deposits() { Fy = System.DateTime.Parse("10/01/"+tmpYear.ToString()), WescAccum = 0, RentalAccum = 0, GolfAccum = 0, VouchersAccum = 0 }
            };
            // initialize recon array
            var reconArray = new List<NewbookSupport.Recon>
            {
                new Recon() { ReconItem = "LockFees", Accum = 0, GL = "0309", MiscTrans = false },
                new Recon() { ReconItem = "TransferFees", Accum = 0, GL = "0358", MiscTrans = false },
                new Recon() { ReconItem = "Events", Accum = 0, GL = "0359", MiscTrans = false },
                new Recon() { ReconItem = "VisitorFees", Accum = 0, GL = "0360", MiscTrans = false },
                new Recon() { ReconItem = "ExtraVehicleFees", Accum = 0, GL = "0361", MiscTrans = false },
                new Recon() { ReconItem = "Propane", Accum = 0, GL = "0320", MiscTrans = false },
                new Recon() { ReconItem = "Storage", Accum = 0, GL = "0356", MiscTrans = false },
                new Recon() { ReconItem = "Misc", Accum = 0, GL = "0925", MiscTrans = false },
                new Recon() { ReconItem = "DamageFees", Accum = 0, GL = "0310", MiscTrans = false },
                new Recon() { ReconItem = "LateFees", Accum = 0, GL = "0311", MiscTrans = false }
//                new Recon() { ReconItem = "Trash Pickup", Accum = 0, GL = "0652", MiscTrans = true }
            };
            // initialize applied deposits array
            var appliedArray = new List<NewbookSupport.Applied>
            {
                 new Applied() { AppliedItem = "SiteDepApp", Accum = 0 },
                 new Applied() { AppliedItem = "RentalDepApp", Accum = 0 },
                 new Applied() { AppliedItem = "GolfDepApp", Accum = 0 },
                 new Applied() { AppliedItem = "VouchersRedSite", Accum = 0 },
                 new Applied() { AppliedItem = "VouchersRedRental", Accum = 0 },
                 new Applied() { AppliedItem = "VouchersRedSiteDep", Accum = 0 },
                 new Applied() { AppliedItem = "VouchersRedRentalDep", Accum = 0 },
                 new Applied() { AppliedItem = "VouchersRedStorage", Accum = 0 }
            };
            // initialize balance transfers array
            var transferArray = new List<NewbookSupport.Transfers>
            {
                 new Transfers() { TranItem = "CampsitesT", Accum = 0 },
                 new Transfers() { TranItem = "RentalsT", Accum = 0 },
                 new Transfers() { TranItem = "StorageT", Accum = 0 },
                 new Transfers() { TranItem = "AnnualT", Accum = 0 },
                 new Transfers() { TranItem = "MHParkT", Accum = 0 },
                 new Transfers() { TranItem = "Other", Accum = 0 },
                 new Transfers() { TranItem = "Forfeits", Accum = 0 },
                 new Transfers() { TranItem = "Vouchers", Accum = 0 },
                 new Transfers() { TranItem = "Guests", Accum = 0 },
                 new Transfers() { TranItem = "GolfCarts", Accum = 0 },
                 new Transfers() { TranItem = "SiteDepositsT", Accum = 0 },
                 new Transfers() { TranItem = "RentalDepositsT", Accum = 0 },
                 new Transfers() { TranItem = "GolfDepositsT", Accum = 0 }
            };
            // initialize manual checks array
            var checkArray = new List<NewbookSupport.Checks>
            {
                 new Checks() { CheckItem = "CampsitesC", Accum = 0 },
                 new Checks() { CheckItem = "RentalsC", Accum = 0 },
                 new Checks() { CheckItem = "GolfC", Accum = 0 },
                 new Checks() { CheckItem = "LTCampsitesC", Accum = 0 },
                 new Checks() { CheckItem = "LTRentalsC", Accum = 0 },
                 new Checks() { CheckItem = "StorageC", Accum = 0 },
                 new Checks() { CheckItem = "AnnualC", Accum = 0 },
                 new Checks() { CheckItem = "MHParkC", Accum = 0 },
                 new Checks() { CheckItem = "SiteDepositsC", Accum = 0 },
                 new Checks() { CheckItem = "RentalDepositsC", Accum = 0 },
                 new Checks() { CheckItem = "GolfDepositsC", Accum = 0 },
                 new Checks() { CheckItem = "OtherC", Accum = 0 }
            };
            // Open the transaction flow and reconciliation files
            XLWorkbook transBook = new XLWorkbook(GenericRoutines.nbfiles.Transflow);
            IXLWorksheet transSheet = transBook.Worksheet(1);
            int rowCount = transSheet.LastRowUsed()!.RowNumber();
            //XLWorkbook reconBook = new XLWorkbook(GenericRoutines.nbfiles.Recon);
            //IXLWorksheet reconSheet = reconBook.Worksheet(1);
            //Spire.Xls.Worksheet specialSheet = SupportRoutines.BuildSpecialReconSheet(reconSheet);
            IXLCell workCell;
            startRow = 0;
            for (int ih = 1; ih <= rowCount; ih++) // Loop until we find the headers row (usually the first row)
            {
                workCell = transSheet.Row(ih).Cell(catCol);
                if (workCell != null && workCell.Value.ToString() == "Category")
                {
                    startRow = ih + 1;
                    break;
                }
            }
            int visCnt = 0, vehdCnt = 0, vehaCnt = 0, wristCnt = 0;
            double visTot = 0, vehdTot = 0, vehaTot = 0, wristTot = 0;
            for (int i = startRow; i <= rowCount; i++) // Loop to the end of the file
            {
                tmpAction = transSheet.Row(i).Cell(1).Value.ToString();
                tmpCat = transSheet.Row(i).Cell(catCol).Value.ToString();
                tmpTrans = transSheet.Row(i).Cell(transCol).Value.ToString();
                tmpClient = transSheet.Row(i).Cell(clientCol).Value.ToString();
                tmpGen = transSheet.Row(i).Cell(genCol).Value.ToString();
                tmpDesc = transSheet.Row(i).Cell(descCol).Value.ToString();
                // Replace blank category if we need to (some small cash transactions may remain blank)
                // Do not seek category for vouchers being redeemed as they are tied to a guest not a booking when purchased
                if (tmpCat == "" && tmpDesc.IndexOf("Balance Transfer for Gift Voucher to Client Account") == -1)
                {
                    tmpCat = SupportRoutines.GetMissingCategory(transSheet, tmpAction, tmpTrans, tmpClient, catCol, transCol);
                }
                tmpCat = tmpCat.ToUpper();  // Make it uppercase, then simplify for all remaining comparisons
                if (tmpCat != "")
                {
                    if (tmpCat != "WESC" && tmpCat != "GUEST" && (tmpCat.IndexOf("VILLA") != -1 || tmpCat.IndexOf("CABIN") != -1 || tmpCat.IndexOf("STANDARD") != -1 ||
                       tmpCat.Substring(0, 5) == "ELITE" || tmpCat.Substring(0, 7) == "PREMIUM" || tmpCat.IndexOf("COTTAGE") != -1 ||
                       (tmpCat.IndexOf("TRAILER") != -1 && tmpCat.IndexOf("STORAGE") == -1)))
                    {
                        tmpCat = "RENTAL";    // Set an all-encompassing value for any type of rental unit
                    }
                    else if (tmpCat == "WATER & ELECTRIC ONLY")
                    {
                        tmpCat = "WESC";
                    }
                }
                // This IF block is necessary to avoid a crash if there are no values in the date fields
                if (transSheet.Row(i).Cell(arrCol).Value.ToString() == "")
                {
                    arrDate = System.DateTime.Parse(GenericRoutines.repDateStr); // Assign report date.  Departure date not needed
                    departDate = arrDate;
                }
                else
                {
                    arrDate = System.DateTime.Parse(System.DateTime.Parse(transSheet.Row(i).Cell(arrCol).Value.ToString()).ToShortDateString());
                    departDate = System.DateTime.Parse(System.DateTime.Parse(transSheet.Row(i).Cell(depCol).Value.ToString()).ToShortDateString());
                }
                double.TryParse(transSheet.Row(i).Cell(amtCol).Value.ToString(), out tmpVal); // attempt conversion to double, ignore if false (cellVal will = 0)
                tmpVal = Math.Round(tmpVal * -1, 2);
                // flowStr is a common string that will be passed to all "Add" routines
                flowStr = tmpAction + " (" + tmpGen + ") for " + tmpCat + "/" + tmpClient + "/" + tmpTrans + "/" + tmpDesc + " for " + string.Format("{0:c}", transSheet.Row(i).Cell(amtCol).Value);
                // Since the Transaction Flow is sorted by actions we can open or close the Departing file if we enter or leave
                // the Refund Check entries
                if (tmpAction.IndexOf("Manual Entry Check Refunds") != -1 && refundChecksActive == false)
                {
                    refundChecksActive = true;
                }
                // 'sheetsToProcess = openSourceFile("Departed List", ThisWorkbook.fixedDownloadsPath, "Bookings_Departing_List_Current_Quarter_" & wrkDay & ".xlsx", "XLSX", "Newbook")
                //  sheetsToProcess = openSourceFile("Departed List", downloadsPath, "Bookings_Departing_List_Current_Quarter_" & wrkDay & ".xlsx", "XLSX", "Newbook")
                //  If sheetsToProcess = -10 Then Exit Sub
                //Set departedBookCurr = ThisWorkbook.srcBook
                //Set departedSheetCurr = ThisWorkbook.srcSheet
                else if (refundChecksActive && tmpAction.IndexOf("Manual Entry Check Refunds") == -1)
                {
                    refundChecksActive = false;
                    //Set departedSheetCurr = Nothing
                    //departedBookCurr.Close False
                    //Set departedBookCurr = Nothing
                }
                if (tmpAction.Contains("Manual Entry") && (tmpAction.Contains("Visa") || tmpAction.Contains("MasterCard") || tmpAction.Contains("Discover") || tmpAction.Contains("AMEX")))
                {
                    string actionString;
                    if (tmpAction.Contains("Payments"))
                    {
                        actionString = tmpAction.Substring(0, tmpAction.IndexOf("Payments"));
                    }
                    else
                    {
                        actionString = tmpAction.Substring(0, tmpAction.IndexOf("Refunds"));
                    }
                    GenericRoutines.UpdateAlerts(100, "CRITICAL ERROR", actionString + "(" + tmpGen + ") " + tmpTrans + " " + tmpClient + ": " + String.Format("{0:C}", tmpVal));
                }
                if (tmpAction.ToUpper().IndexOf("NONE") != -1 || tmpAction.ToUpper().IndexOf("BARTERCARD") != -1) { }  // Ignore these entries
                else if (tmpAction.IndexOf("Manual Entry Check Refunds") != -1) //Refund found - exclude MBTP checks from calculations
                {
                    tmpVal *= -1;
                    if (tmpCat.IndexOf("ANNUAL") != -1)
                    {
                        SupportRoutines.AddCheck(checkArray, "AnnualC", flowStr, tmpVal);
                    }
                    else if (tmpCat.IndexOf("MOBILE") != -1)
                    {
                        SupportRoutines.AddCheck(checkArray, "MHParkC", flowStr, tmpVal);
                    }
                    else if (tmpCat.IndexOf("STORAGE") != -1)
                    {
                        SupportRoutines.AddCheck(checkArray, "StorageC", flowStr, tmpVal);
                    }
                    else if (tmpCat.IndexOf("WHEELCHAIR") != -1 || tmpCat == " ")
                    {
                        SupportRoutines.AddCheck(checkArray, "OtherC", flowStr, tmpVal);
                    }
                    else
                    {
                        if (arrDate > System.DateTime.Parse(GenericRoutines.repDateStr))
                        {
                            SupportRoutines.AddCheck(checkArray, tmpCat.IndexOf("WESC") != -1 ? "SiteDepositsC" : tmpCat.IndexOf("GOLF") != -1 ? "GolfDepositsC": "RentalDepositsC", flowStr, tmpVal);
                        }
                        else
                        {
                            // first we have to see if the booking ever checked in.  If not, even if the arrival date is prior to the report
                            // date the refund still comes out of deposits
                            // Look through the Bookings Departing List report(s) for the booking number.  If no match for the booking is found
                            // the refund comes from deposits
                            bool bookingDeparted = false;
                            string listPath = GenericRoutines.DoesFileExist("", "Bookings_Departing_List_Current_Quarter_", ".xlsx");
                            XLWorkbook listBook = new XLWorkbook(listPath);
                            IXLWorksheet listSheet = listBook.Worksheet(1);
                            int listRowCount = listSheet.LastRowUsed()!.RowNumber();
                            for (int listCounter = 2; listCounter <= listRowCount; listCounter++)
                            {
                                if(listSheet.Row(listCounter).Cell(1).Value.ToString().Substring(0,7) == tmpClient.Substring(9,7))
                                {
                                    bookingDeparted = true;
                                    break;
                                }
                            }
                            listBook.Dispose();
                            // if the booking wasn't found check the prior quarter too
                            if (bookingDeparted == false)
                            {
                                listPath = GenericRoutines.DoesFileExist("", "Bookings_Departing_List_Previous_Quarter_", ".xlsx");
                                listBook = new XLWorkbook(listPath);
                                listSheet = listBook.Worksheet(1);
                                listRowCount = listSheet.LastRowUsed()!.RowNumber();
                                for (int listCounter = 2; listCounter <= listRowCount; listCounter++)
                                {
                                    if (listSheet.Row(listCounter).Cell(1).Value.ToString().Substring(0, 7) == tmpClient.Substring(9, 7))
                                    {
                                        bookingDeparted = true;
                                        break;
                                    }
                                }
                                listBook.Dispose();
                            }
                            // one last check to see if the guest checked in on their arrival date, since Accommodation is in the description
                            // we will loop up to 10 times to see if the booking is on the checked in list beginning on the intended arrival
                            // date. If the guest did check in we set BookingDeparted to true to force the refund against income.
                            if (bookingDeparted == false)
                            {
                                int arrDays;
                                DateTime tmpArrDate = arrDate;
                                for (arrDays = 0; arrDays <= 9; arrDays++)
                                {
                                    bookingDeparted = GenericRoutines.DidGuestArrive("", "Checked_In_List_", ".xlsx", tmpArrDate, tmpClient.Substring(10, 6));
                                    if (bookingDeparted)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        tmpArrDate = arrDate.AddDays(1);
                                    }
                                }
                            }
                            // Booking-specific check added 3/6/25 to overcome procedural error in Newbook 
                            if (bookingDeparted == false || tmpClient.Substring(9, 7) == "#327777")   // never arrived or departed so it gets applied to deposits
                            {
                                SupportRoutines.AddCheck(checkArray, tmpCat.IndexOf("WESC") != -1 ? "SiteDepositsC" : tmpCat.IndexOf("GOLF") != -1 ? "GolfDepositsC" : "RentalDepositsC", flowStr, tmpVal);
                            }
                            else if (tmpCat.IndexOf("GOLF") != -1)  // It's a refund against cart rental income
                            {
                                SupportRoutines.AddCheck(checkArray, "GolfC", flowStr, tmpVal);
                            }
                            else  // check to see if it gets applied to long-term (non-taxable) income or regular income
                            {
                                TimeSpan daysBetween = departDate - arrDate;
                                if (daysBetween.Days >= 90)// Long term rental unit or site
                                {
                                    SupportRoutines.AddCheck(checkArray, tmpCat.IndexOf("WESC") != -1 ? "LTCampsitesC" : "LTRentalsC", flowStr, tmpVal);
                                }
                                else
                                {
                                    SupportRoutines.AddCheck(checkArray, tmpCat.IndexOf("WESC") != -1 ? "CampsitesC" : "RentalsC", flowStr, tmpVal);
                                }
                            }
                        }
                    }
                }
                else
                {
                    bool reconMatchFound;
                    // We look for the presence of the record in the reconciliation file.  If it exists we will process
                    // it later unless we override it and pull it out based on the IF block below.
                    if (tmpAction.IndexOf("Refunds") != -1 && tmpAction.IndexOf("Balance Transfer") == -1 && tmpTrans.IndexOf("Ref #") != -1)
                    {
                        reconMatchFound = SupportRoutines.ValidReconEntryFound(tmpAction, tmpTrans, tmpVal, transCol, transSheet);
                    }
                    else
                    {
                        reconMatchFound = SupportRoutines.ValidReconEntryFound(tmpAction, tmpTrans, tmpVal);
                    }
                    if (tmpCat.IndexOf("STORAGE") != -1)
                    {
                        tmpCat = tmpCat.ToUpper();
                    }
                    tmpDesc = tmpDesc.ToUpper();    // So comparisons aren't case sensitive
                    // Test to see if the value is a multiple of vehicle fee w/tax ($5.60 for FY 21+).
                    // Choose 10x as arbitrary max value
                    // The test is to be done this high in the IF so that it will correctly assign vehicle charges even if the
                    // value in the Description field is Accommodation
                    //                    if (((tmpCat.IndexOf("WESC") != -1 || tmpCat.IndexOf("RENTAL") != -1) && tmpDesc.IndexOf("REFUND") == -1 &&
                                        // (tmpDesc.IndexOf("ACCOMMODATION") != -1 && Math.Abs(tmpVal) <= 10 * vehicleRateDayTax)) || tmpVal > 0) &&
                    if (((tmpCat.IndexOf("WESC") != -1 || tmpCat.IndexOf("RENTAL") != -1) && 
                        ((tmpVal < 0 && tmpDesc.IndexOf("ACCOMMODATION") == -1) || tmpVal > 0) &&
                        tmpDesc.IndexOf("REFUND") == -1 &&
                        Math.Abs(tmpVal) <= 10 * vehicleRateDayTax &&
                        Math.Truncate(tmpVal / vehicleRateDayTax) == Math.Round(tmpVal / vehicleRateDayTax, 2) &&
                        (tmpDesc.IndexOf("DEP") == -1 || tmpDesc.IndexOf("EX") != -1) &&
                        tmpAction.IndexOf("Balance Transf") == -1) || tmpDesc.IndexOf("VEHICLE REFUND") != -1)
                    {
                        reconArray = SupportRoutines.AddRecon(reconArray, "Extra", flowStr, tmpVal);
                        if (tmpDesc.IndexOf("EX") == -1)
                        {
                            SupportRoutines.AddAssumption("Unable to determine intent, assigning to Extra Vehicle Fees from " + flowStr);
                        }
                    }
                    // EVENTS
                    else if (tmpDesc.IndexOf("OYSTER") != -1 || tmpDesc.IndexOf("ACTIV") != -1)
                    {
                        if (reconMatchFound == false) 
                        { 
                            reconArray = SupportRoutines.AddRecon(reconArray, "Events", flowStr, tmpVal); 
                        }
                        else // Skip it, we'll grab it in the recon file
                        { 
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); 
                        }    
                    }
                    // EMPLOYEE SITE
                    else if (tmpCat.IndexOf("EMPLOYEE") != -1 || (tmpCat.IndexOf("ANNUAL") != -1 && tmpDesc.IndexOf("EMPLOYEE") != -1))
                    {
                        if (tmpDesc.IndexOf("TRAILER SALES") != -1)
                        {
                            reconArray = SupportRoutines.AddRecon(reconArray, "Trailer Sales", flowStr, 0, "0319");
                            if (reconMatchFound == true)
                            // Skip it, we'll grab it in the recon file
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal);
                            }
                            else
                            {
                                reconArray = SupportRoutines.AddRecon(reconArray, "Trailer Sales", flowStr, tmpVal);
                            }
                        }
                        else
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Employee", flowStr, tmpVal);
                            if (tmpAction.IndexOf("Balance Transf") != -1) { transferArray = SupportRoutines.AddTransfer(transferArray, "AnnualT", flowStr, tmpVal); }
                        }
                    }
                    // PROPANE SALES
                    else if (tmpDesc.IndexOf("PROPANE") != -1)
                    {
                        if (reconMatchFound == false) { reconArray = SupportRoutines.AddRecon(reconArray, "Propane", flowStr, tmpVal); }
                        else { revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); }    // Skip it, we'll grab it in the recon file
                    }
                    // TRASH PICKUP
                    else if (tmpDesc.IndexOf("TRASH") != -1)
                    {
                        reconArray = SupportRoutines.AddRecon(reconArray, "Trash Pickup", flowStr, 0, "0652");  // insert placeholder in array
                        if (reconMatchFound == true)
                        // Skip it, we'll grab it in the recon file
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal);
                        }
                        else
                        {
                            reconArray = SupportRoutines.AddRecon(reconArray, "Trash Pickup", flowStr, tmpVal, "0652");
                        }
                    }
                    // MISCELLANEOUS - FAXES and COPIES and LOST KEYS and Tree work (added 12/3/23)
                    else if (tmpDesc.IndexOf("FAX") != -1 || tmpDesc.IndexOf("COPIES") != -1 ||
                                tmpDesc.IndexOf("LOST") != -1 || tmpDesc.IndexOf("TREE") != -1)
                    {
                        if (reconMatchFound) // We're skipping this record on purpose, we'll get the money from the recon file
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal);
                        }
                        else
                        {
                            reconArray = SupportRoutines.AddRecon(reconArray, "Misc", flowStr, tmpVal);
                        }
                    }
                    // MISCELLANEOUS STORAGE FROM LEASED LOTS
                    else if ((tmpCat.IndexOf("ANNUAL") != -1 || tmpCat.IndexOf("MOBILE") != -1) && tmpDesc.IndexOf("STORAGE") != -1)
                    {
                        if (reconMatchFound == false) { reconArray = SupportRoutines.AddRecon(reconArray, "Storage", flowStr, tmpVal); }
                        else { revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); }   // Skip it, we'll grab it in the recon file
                    }
                    // VISITOR FEES BEGINS
                    // VF Section 1
                    else if (tmpDesc.IndexOf("VISITOR") != -1 || tmpDesc.IndexOf("VISTOR") != -1 || tmpDesc.IndexOf("DAY") != -1 ||
                            tmpDesc.IndexOf("PASS") != -1 || tmpDesc.IndexOf("WRIST") != -1 ||
                            (tmpClient == "Cash Account" && tmpDesc == "ACCOMMODATION" &&
                             tmpVal % 2 == 0 && Math.Abs(tmpVal) >= visitorRateBase && tmpAction.IndexOf("Balance Transf") == 0))
                    {
                        if (tmpDesc.IndexOf("WRIST") != -1)
                        {
                            wristTot += tmpVal;
                            wristCnt += (int)Math.Truncate(tmpVal / wristbandRate);
                            reconArray = SupportRoutines.AddRecon(reconArray, "VisitorWRIST", flowStr, tmpVal);
                        }
                        else
                        {
                            visTot += tmpVal;
                            visCnt += (int)Math.Truncate(tmpVal / visitorRateTax);
                            reconArray = SupportRoutines.AddRecon(reconArray, "Visitor", flowStr, tmpVal);
                        }
                    } // VF Section 1 ENDS
                    // VF Section 2
                    else if ((tmpCat.IndexOf("ANNUAL") != -1 || tmpCat.IndexOf("MOBILE") != -1) &&
                             (((Math.Round(tmpVal / vehicleRateDayTax, 2) != (int)Math.Truncate(tmpVal / vehicleRateDayTax)) &&
                                Math.Abs(tmpVal) <= vehicleRateYrTax * 2 && tmpDesc.IndexOf("MISC") == -1 &&
                                tmpDesc.IndexOf("NOT VEHICLE") == -1) || tmpDesc.IndexOf("EXTRA") != -1))
                    {
                        if (Math.Abs(tmpVal) >= vehicleRateYrBase)
                        {
                            vehaTot += tmpVal;
                            vehaCnt = (int)(vehaCnt + Math.Truncate(tmpVal / vehicleRateYrBase));
                            reconArray = SupportRoutines.AddRecon(reconArray, "VisitorEXYA", flowStr, tmpVal);
                        }
                        else
                        {
                            vehdTot += tmpVal;
                            vehdCnt = (int)(vehdCnt + Math.Truncate(tmpVal / vehicleRateDayBase));
                            reconArray = SupportRoutines.AddRecon(reconArray, "VisitorEXDA", flowStr, tmpVal);
                        }
                    } // VF Section 2 ENDS
                      // VF Section 3

                    else if ((tmpDesc.IndexOf("ACCOMMODATION") != -1 || tmpDesc.IndexOf("EXTRA") != -1) &&
                             ((tmpVal / vehicleRateDayTax == Math.Truncate(tmpVal / vehicleRateDayTax)) ||
                              (tmpVal / vehicleRateYrTax == Math.Truncate(tmpVal / vehicleRateYrTax))) &&
                             tmpTrans.IndexOf("Refunds Raised") == -1 && tmpDesc.IndexOf("NOT VEHICLE") == -1 &&
                             tmpAction.IndexOf("Balance Transfer") == -1 && Math.Abs(tmpVal) < 200)  // 200 is an arbitrary value
                    {
                        if (Math.Abs(tmpVal) >= vehicleRateYrBase)
                        {
                            vehaTot += tmpVal;
                            vehaCnt += (int)Math.Truncate(tmpVal / vehicleRateYrBase);
                            reconArray = SupportRoutines.AddRecon(reconArray, "VisitorEXYA", flowStr, tmpVal);
                            //reconArray = SupportRoutines.AddRecon(reconArray, "Extra Vehicle Fees", flowStr, tmpVal);
                        }
                        else
                        {
                            vehdTot += tmpVal;
                            vehdCnt += (int)Math.Truncate(tmpVal / vehicleRateDayBase);
                            reconArray = SupportRoutines.AddRecon(reconArray, "VisitorEXDA", flowStr, tmpVal);
                            //reconArray = SupportRoutines.AddRecon(reconArray, "Extra Vehicle Fees", flowStr, tmpVal);
                        }
                    } // VF Section 3 ENDS
                    // VF Section 4
                    else if ((tmpCat.IndexOf("WESC") != -1 || tmpCat.IndexOf("RENTAL") != -1) &&
                            (tmpDesc.IndexOf("ACCOMMODATION") != -1 || tmpDesc.IndexOf("EXTRA") != -1) &&
                            tmpVal / visitorRateTax == (int)Math.Truncate(tmpVal / visitorRateTax) &&
                            tmpTrans.IndexOf("Refunds Raised") == -1 && tmpAction.IndexOf("Balance Transf") == -1 &&
                            Math.Abs(tmpVal) <= visitorRateTax * 7) // 7 is arbitrary value to keep it under lock fee
                    {
                        reconArray = SupportRoutines.AddRecon(reconArray, "Visitor", flowStr, tmpVal);
                        visTot += tmpVal;
                        visCnt += (int)Math.Truncate(tmpVal / visitorRateTax);
                    } // VF Section 4 ENDS
                    // VISITOR FEES ENDS
                    // BEACH WHEELCHAIR
                    else if (tmpCat == "BEACH WHEELCHAIR" || tmpDesc.IndexOf("CHAIR") != -1 || tmpDesc.IndexOf("WHEEL") != -1)
                    {
                        revenueArray = SupportRoutines.AddRevenue(revenueArray, "Misc", flowStr, tmpVal);
                    }
                    // TRANSFER FEE
                    else if (tmpDesc.IndexOf("TRANSFER FEE") != -1)
                    {
                        if (reconMatchFound == false) { reconArray = SupportRoutines.AddRecon(reconArray, "TransferFees", flowStr, tmpVal); }
                        else { revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); }  // Skip it, we'll grab it in the recon file
                    }
                    // LOCK FEE
                    else if (tmpDesc.IndexOf("LOCK") != -1)
                    {
                        if (reconMatchFound) { revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); } // Skip it, we'll grab it in the recon file
                        else { reconArray = SupportRoutines.AddRecon(reconArray, "LockFees", flowStr, tmpVal); }
                    }
                    // LATE FEE
                    else if (tmpDesc.IndexOf("LATE") != -1)
                    {
                        if (reconMatchFound == false) { reconArray = SupportRoutines.AddRecon(reconArray, "Late", flowStr, tmpVal); }
                        else { revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); } // Skip it, we'll grab it in the recon file
                    }
                    // DAMAGE FEE
                    else if (tmpDesc.IndexOf("DAMAGE") != -1)
                    {
                        if (reconMatchFound == false) { reconArray = SupportRoutines.AddRecon(reconArray, "Damage", flowStr, tmpVal); }
                        else { revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); } // Skip it, we'll grab it in the recon file
                    }
                    // ANNUAL LEASE AND MOBILE HOME
                    else if (tmpCat.IndexOf("ANNUAL") != -1 || tmpCat.IndexOf("MOBILE") != -1)
                    {
                        if (tmpDesc.IndexOf("ANNUAL LEASE") != -1 || tmpDesc == "ACCOMMODATION" ||
                            tmpDesc.IndexOf("MOBILE HOME") != -1 || tmpDesc.IndexOf("BALANCE TRANSFER") != -1)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("ANNUAL") != -1 ? "Annual" : "MHPark", flowStr, tmpVal);
                            if (tmpAction.IndexOf("Balance Transf") != -1) { transferArray = SupportRoutines.AddTransfer(transferArray, tmpCat.IndexOf("ANNUAL") != -1 ? "AnnualT" : "MHParkT", flowStr, tmpVal); }
                        }
                        else if (Math.Abs(tmpVal) > 150) // Arbitrary value; above this amount will be considered a normal payment
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("ANNUAL") != -1 ? "Annual" : "MHPark", flowStr, tmpVal);
                            SupportRoutines.AddAssumption("Unable to determine intent, assigning to " + (tmpCat.IndexOf("ANNUAL") != -1 ? "Annual Lease" : "Mobile Home") + " from " + flowStr);
                        }
                        else if (tmpDesc.IndexOf("STORAGE") != -1 && reconMatchFound == false) { revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal); }
                        else if (reconMatchFound == false)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Misc", flowStr, tmpVal);
                            GenericRoutines.UpdateAlerts(1, "Informational", "(1)Unable to determine intent, assigning to Misc from " + flowStr);
                        }
                    } // ANNUAL LEASE AND MOBILE HOME ENDS
                    // GOLF CARTS
                    else if (tmpCat.IndexOf("GOLF CART RENTAL") != -1)
                    {
                        int jCnt = 3;
                        for (int ii = 0; ii < 3; ii++) // We need to know the fiscal year in case deposits are involved
                        {              // The slot is same for rentals and campsites so the use of the Rentals array is purely arbitrary
                            if (depositsArray[ii].Fy <= arrDate)
                            {
                                jCnt = ii;
                                break;
                            }
                        }
                        if (jCnt == 3)    //   This indicates a possible error.  Put the money in the current FY and report a warning
                        {
                            jCnt = 2;
                            TimeSpan daysBetween = depositsArray[2].Fy - departDate;
                            if (daysBetween.Days < 0) // We can't ignore the error if departure is also in previous FY
                            {
                                if (SupportRoutines.CheckFYChange(tmpClient) == false)
                                {
                                    GenericRoutines.UpdateAlerts(1, "WARNING", "PRIOR FY! Payment from " + flowStr + " was applied to a past reservation. Current FY used.");
                                }
                            }
                        }
                        if (tmpAction.IndexOf("Balance Transf") != -1)
                        {
                            transferArray = SupportRoutines.AddTransfer(transferArray, "GolfCarts", flowStr, tmpVal);
                            if (arrDate >= System.DateTime.Parse(GenericRoutines.repDateStr)) { depositsArray = SupportRoutines.AddDeposit(depositsArray, "Golf", jCnt, flowStr, tmpVal); }
                            else
                            {
                                int returnedVal = SupportRoutines.CheckForCancel(tmpClient);
                                if (returnedVal == 1) { depositsArray = SupportRoutines.AddDeposit(depositsArray, "Golf", jCnt, flowStr, tmpVal); } // If cancel back out of deposits even if earlier date
                                else if (returnedVal == 0) { revenueArray = SupportRoutines.AddRevenue(revenueArray, "GolfCartRentals", flowStr, tmpVal); }
                                else { return; }; // stop processing. Alert written in CheckForCancel
                            }
                        }
                        else
                        {
                            if (arrDate == System.DateTime.Parse(GenericRoutines.repDateStr) && tmpTrans.ToUpper().IndexOf("REFUND") == -1) 
                            { 
                                if (reconMatchFound == false || SameDayArrival(tmpClient.Substring(10,6)) || tmpDesc.IndexOf("WALKIN") != -1)
                                {
                                    appliedArray = SupportRoutines.AddApplied(appliedArray, "GolfDepApp", flowStr, tmpVal);
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, "GolfCartRentals", flowStr, tmpVal);
                                }
                            }
                            depositsArray = SupportRoutines.AddDeposit(depositsArray, "Golf", jCnt, flowStr, tmpVal);
                        }
                    } // GOLF CARTS ENDS
                    // WESC AND RENTALS BEGINS
                    else if (tmpCat.IndexOf("WESC") != -1 || tmpCat.IndexOf("RENTAL") != -1)
                    {
                        int jCnt = 3;
                        for (int ii = 0; ii < 3; ii++) // We need to know the fiscal year in case deposits are involved
                        {              // The slot is same for rentals and campsites so the use of the Rentals array is purely arbitrary
                            if (depositsArray[ii].Fy <= arrDate)
                            {
                                jCnt = ii;
                                break;
                            }
                        }
                        if (jCnt == 3)    //   This indicates a possible error.  Put the money in the current FY and report a warning
                        {
                            jCnt = 2;
                            TimeSpan daysBetween = depositsArray[2].Fy - departDate;
                            if (daysBetween.Days < 0) // We can't ignore the error if departure is also in previous FY
                            {
                                if (SupportRoutines.CheckFYChange(tmpClient) == false)
                                {
                                    GenericRoutines.UpdateAlerts(1, "WARNING", "PRIOR FY! Payment from " + flowStr + " was applied to a past reservation. Current FY used.");
                                }
                            }
                        }
                        if (tmpAction.IndexOf("Balance Transf") != -1)
                        {
                            if (tmpClient.ToUpper().IndexOf("FORFEIT") != -1)
                            {
                                if (tmpVal != 30) // Move from deposits to revenue except for lock fees
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("WESC") != -1 ? "Campsites" : "Rentals", flowStr, tmpVal);
                                    transferArray = SupportRoutines.AddTransfer(transferArray, tmpCat.IndexOf("WESC") != -1 ? "CampsitesT" : "RentalsT", flowStr, tmpVal);
                                }
                                else
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); // Skip lock fees, we've already claimed them
                                }
                            }
                            else if (tmpDesc.IndexOf("FOR GIFT VOUCHER FROM CLIENT") != -1)
                            {
                                if (arrDate >= System.DateTime.Parse(GenericRoutines.repDateStr))
                                {
                                    appliedArray = SupportRoutines.AddApplied(appliedArray, tmpCat.IndexOf("WESC") != -1 ? "VouchersRedSiteDep" : "VouchersRedRentalDep", flowStr, tmpVal);
                                    depositsArray = SupportRoutines.AddDeposit(depositsArray, tmpCat.IndexOf("WESC") != -1 ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                                    transferArray = SupportRoutines.AddTransfer(transferArray, tmpCat.IndexOf("WESC") != -1 ? "SiteDepositsT" : "RentalDepositsT", flowStr, tmpVal);
                                }
                                else
                                {
                                    appliedArray = SupportRoutines.AddApplied(appliedArray, tmpCat.IndexOf("WESC") != -1 ? "VouchersRedSite" : "VouchersRedRental", flowStr, tmpVal);
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("WESC") != -1 ? "Campsites" : "Rentals", flowStr, tmpVal);
                                    transferArray = SupportRoutines.AddTransfer(transferArray, tmpCat.IndexOf("WESC") != -1 ? "CampsitesT" : "RentalsT", flowStr, tmpVal);
                                }
                            }
                            else if (tmpDesc.IndexOf("BALANCE TRANSFER FROM ACCOUNT") != -1 || tmpDesc.IndexOf("BALANCE TRANSFER TO ACCOUNT") != -1 ||
                                     tmpDesc.IndexOf("BALANCE TRANSFER FROM CLIENT ACCOUNT") != -1 || tmpDesc.IndexOf("BALANCE TRANSFER TO CLIENT ACCOUNT") != -1)
                            {
                                if (Math.Abs(tmpVal) == 30 && tmpDesc.IndexOf("EXCEPTION") == -1) // Assume it's a Lock fee transfer or forfeit so skip it
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); // Lock fee transfer or forfeit so skip it
                                }
                                else if (arrDate >= System.DateTime.Parse(GenericRoutines.repDateStr))
                                {
                                    depositsArray = SupportRoutines.AddDeposit(depositsArray, tmpCat.IndexOf("WESC") != -1 ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                                    transferArray = SupportRoutines.AddTransfer(transferArray, tmpCat.IndexOf("WESC") != -1 ? "SiteDepositsT" : "RentalDepositsT", flowStr, tmpVal);
                                }
                                else
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("WESC") != -1 ? "Campsites" : "Rentals", flowStr, tmpVal);
                                    transferArray = SupportRoutines.AddTransfer(transferArray, tmpCat.IndexOf("WESC") != -1 ? "CampsitesT" : "RentalsT", flowStr, tmpVal);
                                }
                            }
                            else if (tmpDesc.IndexOf("CANCEL") != -1)
                            {
                                transferArray = SupportRoutines.AddTransfer(transferArray, tmpCat.IndexOf("WESC") != -1 ? "SiteDepositsT" : "RentalDepositsT", flowStr, tmpVal);
                                depositsArray = SupportRoutines.AddDeposit(depositsArray, tmpCat.IndexOf("WESC") != -1 ? "WESC" : "Rentals", 2, flowStr, tmpVal);
                            }
                            else if ((tmpTrans.IndexOf("Voided Payments Voided") != -1 && tmpDesc == "") ||
                                     (tmpTrans.IndexOf("Voided Refunds Voided") != -1 && tmpDesc == "") ||
                                     (tmpTrans.IndexOf("Refunds Raised") != -1 && tmpDesc == "") ||
                                     (tmpTrans.IndexOf("Payments Raised") != -1 && tmpDesc == ""))
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("WESC") != -1 ? "Campsite" : "Rental", flowStr, tmpVal);
                            }
                            else
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "DROPPED", flowStr, tmpVal); // If we got here we didn't process the balance transfer correctly
                            }
                        }
                        else if ((tmpDesc.IndexOf("STORAGE") != -1 || tmpDesc.IndexOf("TRAILER MOVE") != -1 || tmpDesc == "SERVICE FEE" ||
                                  tmpDesc == "MOVING FEE" || tmpDesc.IndexOf("TOW") != -1) && reconMatchFound == false)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal);
                        }
                        else if (tmpDesc.IndexOf("EMPLOYEE") != -1)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Employee", flowStr, tmpVal);
                        }
                        //                        else if (tmpDesc.IndexOf("DEP") != -1 || tmpDesc.Substring(0, 7) == "BOOKING" || tmpDesc.IndexOf("RESTORED CREDIT CARD") != -1 ||
                        //                                 tmpDesc.Substring(0, 11) == "RŽSERVATION" || tmpDesc.Substring(0, 11) == "RÉSERVATION" ||
                        //                                 tmpDesc.IndexOf("REFUND") != -1 || (tmpDesc.IndexOf("ACCOMMODATION") != -1 && arrDate >= System.DateTime.Parse(GenericRoutines.repDateStr)))
                        else if (tmpDesc.IndexOf("DEP") != -1 || tmpDesc.IndexOf("BOOKING") != -1 || tmpDesc.IndexOf("RESTORED CREDIT CARD") != -1 ||
                                 tmpDesc.IndexOf("RŽSERVATION") != -1 || tmpDesc.IndexOf("RÉSERVATION") != -1 ||
                                 tmpDesc.IndexOf("REFUND") != -1 || (tmpDesc.IndexOf("ACCOMMODATION") != -1 && arrDate >= System.DateTime.Parse(GenericRoutines.repDateStr)))
                        {
                            if (tmpVal == (tmpCat.IndexOf("WESC") != -1 ? siteDeposit : rentalDeposit) + lockFee && System.DateTime.Parse(GenericRoutines.repDateStr) >= System.DateTime.Parse("10/13/2022"))
                            {
                                if (tmpCat.IndexOf("WESC") != -1)
                                {
                                    depositsArray = SupportRoutines.AddDeposit(depositsArray, "WESC", jCnt, flowStr, siteDeposit);
                                }
                                else
                                {
                                    depositsArray = SupportRoutines.AddDeposit(depositsArray, "Rentals", jCnt, flowStr, rentalDeposit);
                                }
                                reconArray = SupportRoutines.AddRecon(reconArray, "LockFees", flowStr, lockFee);
                            }
                            else if (tmpDesc.IndexOf("RESTORED CREDIT CARD") != -1 && tmpVal == 30 && System.DateTime.Parse(GenericRoutines.repDateStr) >= System.DateTime.Parse("10/13/2022"))
                            {
                                reconArray = SupportRoutines.AddRecon(reconArray, "LockFees", flowStr, tmpVal);
                            }
                            else
                            {
                                if (arrDate == System.DateTime.Parse(GenericRoutines.repDateStr) && tmpTrans.ToUpper().IndexOf("REFUND") == -1 &&
                                    (tmpDesc.IndexOf("BOOKING") != -1 || tmpDesc.IndexOf("ACCOMMODATION") != -1 || tmpDesc.IndexOf("DEP") != -1)) // Same day checkin but still treat as a deposit
                                {
                                    depositsArray = SupportRoutines.AddDeposit(depositsArray, tmpCat.IndexOf("WESC") != -1 ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                                    if (tmpDesc.IndexOf("ACCOMMODATION") != -1 && tmpTrans.ToUpper().IndexOf("REFUND") == -1)  // Extending existing stay
                                    {
                                        tmpVal *= -1; // Back it out what we just added for same day so it has no affect on deposits
                                        depositsArray = SupportRoutines.AddDeposit(depositsArray, tmpCat.IndexOf("WESC") != -1 ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                                        tmpVal *= -1; // Restore original value for revenue and CC/cash accumulators
                                        revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("WESC") != -1 ? "Campsite" : "Rental", flowStr, tmpVal);
                                    }
                                }

                                //  Original IF block altered on 11/12/24
                                //                                else if ((arrDate == System.DateTime.Parse(GenericRoutines.repDateStr) && tmpTrans.ToUpper().IndexOf("REFUND") == -1) ||
                                //                                         (arrDate < System.DateTime.Parse(GenericRoutines.repDateStr) && tmpTrans.ToUpper().IndexOf("REFUND") != -1) &&
                                //                                          tmpDesc.IndexOf("DEPOSIT") == -1)  // Same day checkin, or late checkin that isn't a deposit refund, not a deposit
                                else if ((arrDate == System.DateTime.Parse(GenericRoutines.repDateStr) && tmpTrans.ToUpper().IndexOf("REFUND") == -1) ||
                                         (arrDate < System.DateTime.Parse(GenericRoutines.repDateStr) && tmpTrans.ToUpper().IndexOf("REFUND") != -1) &&
                                          tmpDesc.IndexOf("DEPOSIT") == -1)  // Same day checkin, or late checkin that isn't a deposit refund, not a deposit
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("WESC") != -1 ? "Campsite" : "Rental", flowStr, tmpVal);
                                }
// else if block added 2/5/25 to handle refunds of security deposits
                                else if (departDate <= System.DateTime.Parse(GenericRoutines.repDateStr) && tmpTrans.ToUpper().IndexOf("REFUND") != -1 &&
                                         tmpDesc.IndexOf("DEPOSIT") != -1 && tmpVal == -200)
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("WESC") != -1 ? "Campsite" : "Rental", flowStr, tmpVal);
                                }
                                else
                                {

                                    depositsArray = SupportRoutines.AddDeposit(depositsArray, tmpCat.IndexOf("WESC") != -1 ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                                }
                            }
                        }
                        else if (tmpDesc.IndexOf("ACCOMMODATION") != -1) // *****
                        {
                            int returnedVal = SupportRoutines.CheckForCancel(tmpClient);
                            // The IF check will assign any cancellations refunds to deposits and not back them out of income
                            if (tmpTrans.ToUpper().IndexOf("REFUND") != -1 && returnedVal == 1)
                            {
                                depositsArray = SupportRoutines.AddDeposit(depositsArray, tmpCat.IndexOf("WESC") != -1 ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                            }
                            else
                            {
                                TimeSpan daysBetween = departDate - arrDate;
                                if (daysBetween.Days >= 90)
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("WESC") != -1 ? "LTSites" : "LTUnits", flowStr, tmpVal);
                                }
                                else
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("WESC") != -1 ? "Campsite" : "Rental", flowStr, tmpVal);
                                }
                            }
                        }
                        else if (tmpDesc.IndexOf("GOLF") != -1)
                        {
                            depositsArray = SupportRoutines.AddDeposit(depositsArray, "Golf", 0, flowStr, tmpVal);
                        }
                        else if (tmpDesc.IndexOf("VEH") != -1)
                        {
                            reconArray = SupportRoutines.AddRecon(reconArray, "Extra", flowStr, tmpVal);
                        }
                        else if (reconMatchFound == false)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Misc", flowStr, tmpVal);
                            SupportRoutines.AddAssumption("Unable to determine intent, assigning to Misc from " + flowStr);
                        }
                        else    // We're skipping this record on purpose, we SHOULD get the money from the recon file
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal);
                        }
                    } // WESC AND RENTALS ENDS
                    // STORAGE
                    else if (tmpCat != "" && tmpCat != "GUEST" && (tmpCat.Substring(0, 7) == "STORAGE" || tmpCat == "FRONT PARKING LOT")) // Check for Storage transactions we don't grab from the recon file
                    {
                        if (reconMatchFound)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); // We're skipping this record on purpose, we'll get the money from the recon file
                        }
                        else if (tmpAction.IndexOf("Balance Transf") != -1)
                        {
                            transferArray = SupportRoutines.AddTransfer(transferArray, "Storage", flowStr, tmpVal);
                            if (tmpDesc.IndexOf("FOR GIFT VOUCHER FROM CLIENT") != -1 || tmpDesc.IndexOf("BALANCE TRANSFER TO ACCOUNT") != -1 ||
                                tmpDesc.IndexOf("BALANCE TRANSFER FROM ACCOUNT") != -1 || tmpDesc.IndexOf("BALANCE TRANSFER TO CLIENT ACCOUNT") != -1 ||
                                tmpDesc.IndexOf("BALANCE TRANSFER FROM CLIENT ACCOUNT") != -1)
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal);
                                if (tmpDesc.IndexOf("FOR GIFT VOUCHER FROM CLIENT") != -1)
                                {
                                    appliedArray = SupportRoutines.AddApplied(appliedArray, "VouchersRedStorage", flowStr, tmpVal);
                                }
                            }
                            if (tmpDesc.IndexOf("FOR GIFT VOUCHER TO CLIENT") != -1)
                            {
                                tmpVal *= -1;    //reverse it arithmetically so it prints correctly on the daily report
                                appliedArray = SupportRoutines.AddApplied(appliedArray, "VouchersRedStorage", flowStr, tmpVal);
                                transferArray = SupportRoutines.AddTransfer(transferArray, "StorageT", flowStr, tmpVal);
                            }
                        }
                        else if (tmpCat == "FRONT PARKING LOT" || tmpDesc.IndexOf("STOR") != -1 || tmpDesc == "MISC STORAGE" || tmpDesc == "ONLINE PAYMENT" || tmpDesc.IndexOf("RÉSERVATION") != -1 ||
                                tmpDesc.IndexOf("RESTORED CREDIT CARD") != -1 || tmpDesc.IndexOf("REFUND") != -1 || tmpDesc.Substring(0, 7) == "BOOKING" || tmpDesc.IndexOf("MOVE") != -1)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal);
                        }
                        //                                 (((tmpDesc.IndexOf("DEPOSIT") != -1 || tmpDesc.IndexOf("ACCOMMODATION") != -1) && tmpVal - Math.Round(tmpVal, 0) == 0) ||

                        else if (tmpCat.Substring(0, 7) == "STORAGE" &&
                                 (((tmpDesc.IndexOf("DEPOSIT") != -1 || tmpDesc.IndexOf("ACCOMMODATION") != -1)) ||
                                  tmpDesc.IndexOf("REFUND") != -1))
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal);
                            SupportRoutines.AddAssumption("Unable to determine intent, assigning to Storage from " + flowStr);
                        }
                        else
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Misc", flowStr, tmpVal);
                            GenericRoutines.UpdateAlerts(1, "Informational", "(3)Unable to determine intent, assigning to Misc from " + flowStr);
                        }
                    } // STORAGE ENDS
                    // GIFT VOUCHERS BOUGHT
                    else if (tmpCat == "" && tmpDesc == "GIFT VOUCHER PAYMENT")
                    {
                        depositsArray = SupportRoutines.AddDeposit(depositsArray, "Vouchers", 2, flowStr, tmpVal);
                    }
                    // GIFT VOUCHERS TRANSFERED TO BE USED
                    else if ((tmpCat == "" && tmpDesc.IndexOf("BALANCE TRANSFER FOR GIFT VOUCHER TO CLIENT ACCOUNT") != -1) ||
                           (tmpCat == "GUEST" && tmpDesc.IndexOf("BALANCE TRANSFER FOR GIFT VOUCHER FROM CLIENT ACCOUNT") != -1))
                    {
                        transferArray = SupportRoutines.AddTransfer(transferArray, "Vouchers", flowStr, tmpVal);
                    }
                    // RECORDS NOT PROCESSED BECAUSE THEY ARE IN RECON FILE
                    else if (reconMatchFound)
                    {
                        revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); // This record was deliberately not processed
                    }
                    // RECORDS NOT PROCESSED THAT SHOULD HAVE BEEN
                    else
                    {
                        if (tmpCat == "" && tmpClient.ToUpper().IndexOf("GUEST") != -1)
                        {
                            if(tmpDesc.ToUpper().IndexOf("DEPOSIT") != -1)  // Assume it's going to be a current FY site deposit
                            {
                                depositsArray = SupportRoutines.AddDeposit(depositsArray, "WESC", 2, flowStr, tmpVal);
                            }
                            else if (tmpDesc.ToUpper().IndexOf("STORAGE") != -1)
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal);
                            }
                            else
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "GUEST PAYMENT DROPPED TO BOTTOM", flowStr, tmpVal);
                            }
                        }
                        else
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "DROPPED TO BOTTOM", flowStr, tmpVal); // This record fell through and should not have
                        }
                    }
                } // END OF ELSE BLOCK
                if (tmpAction.Substring(0, 13) == "Authorize.Net")
                {
                    if (tmpAction.IndexOf("AMEX") != -1)
                    {
                        totAmex += tmpVal;
                    }
                    else
                    {
                        totOtherCC += tmpVal;
                    }
                }
                else if (tmpAction.IndexOf("Cash") != -1 || tmpAction.IndexOf("Check Payments") != -1)
                {
                    totCash += tmpVal;
                }
                else if (tmpAction.IndexOf("Manual Entry") != -1 &&
                        (tmpAction.IndexOf("Visa") != -1 || tmpAction.IndexOf("Discover") != -1 ||
                         tmpAction.IndexOf("MasterCard") != -1 || tmpAction.IndexOf("AMEX") != -1))
                {
                    GenericRoutines.UpdateAlerts(1, "CRITICAL ERROR!", flowStr);
                }
            }// END OF THE TRANSACTION FILE READING LOOP
             // Look for specific inventory items
            string pymtResult;
            //Set tmpRng = ThisWorkbook.thisSheet.Range("B:B").Find("Miscellaneous Receipts", lookat:=xlWhole)
            //lastMiscRow = tmpRng.Row
            foreach (SpecialRecon item in SupportRoutines.specialReconArray)
            {
                // look for valid GL code to check, exclude visitor fees, vehicles, golf carts, lost keys
                if ((item.Gl != "0360" && item.Gl != "0361" && item.Gl is not null && item.Gl.Substring(0, 2) != "07") || (item.Gl == "1018" || item.Gl == "1020"))
                    //if ((item.gl != "0360" && item.gl != "0361" && item.gl != "0362" && item.gl.Substring(0, 2) != "07") || (item.gl == "1018" || item.gl == "1020"))
                {
                    if (item.Gl == "0362")
                    {
                        item.Gl = item.Gl;
                    }
                    string pymtNum;
                    if (item.Recon_item is not null && item.Recon_item.IndexOf("Unallocated") != -1)
                    {
                        pymtNum = item.Recon_item.Substring(9, item.Recon_item.IndexOf(" Unalloc") - 9);
                    }
                    else if (item.Desc == "Unallocated Payments" && item.Recon_item is not null)
                    {
                        pymtNum = item.Recon_item.Substring(item.Recon_item.IndexOf("#"), item.Recon_item.IndexOf(" ", item.Recon_item.IndexOf("#")) - item.Recon_item.IndexOf("#"));
                    }
                    else if (item.Recon_item is not null && item.Recon_item.IndexOf(" Alloc") != -1)
                    {
                        pymtNum = item.Recon_item.Substring(9, item.Recon_item.IndexOf(" Alloc") - 9);
                    }
                    else
                    {
                        pymtNum = item.Recon_item!.Substring(item.Recon_item.IndexOf("#"), item.Recon_item.IndexOf(" ", item.Recon_item.IndexOf("#")) - item.Recon_item.IndexOf("#"));
                    }
                    double pymtVal;
                    pymtVal = Math.Round(item.Amount,2);
                    pymtResult = SupportRoutines.PaymentRaised(transSheet, item.Gl, pymtNum, pymtVal * -1, item.Recon_item, 1);  // Look for matching Payments Raised entry in Transaction Flow,
                    if (pymtResult != "NO") // ignore this line if result is "NO"
                    {
                        //jCnt = 0
                        if (item.Gl == "1018" || item.Gl == "1020") // Correction for original GL codes used in Newbook
                        {
                            item.Gl = "0356";
                        }
                        //                    for (int jCnt = 0; jCnt <= SupportRoutines.reconArray.Count; jCnt++)
                        bool matchFound = false;
                        foreach (Recon reconArrItem in reconArray)
                        {
                            if (reconArrItem.GL == "")
                            {
                                matchFound = true;
                                break;
                            }
                            else if (item.Gl == reconArrItem.GL)
                            {
                                matchFound = true;
                                reconArray = SupportRoutines.AddRecon(reconArray, reconArrItem.ReconItem == "" ? reconArrItem.GL : reconArrItem.ReconItem, item.Gl + " " + item.Client + " " + item.Recon_item + " " + (item.Amount * -1).ToString("C"), item.Amount);
                                break;
                            }
                        }
                        if (matchFound == false)   // Add this GL code to the recon array list
                        {
                            reconArray.Add(new Recon { ReconItem = item.Recon_item, Accum = item.Amount, GL = item.Gl });
                        }
                        //If tmpGL = "0319" Then
                        //    reconArray(jCnt, 2) = specialSheet.Range("D" & iCnt).Formula + "-" + _
                        //        Mid(specialSheet.Range("B" & iCnt).Formula, InStr(specialSheet.Range("B" & iCnt).Formula, ")") + 2)
                    }
                }
            } // END OF specific inventory items
              // Now we loop through the recon array and process any non-zero values
            foreach (Recon reconArrItem in reconArray)
            {
                if (reconArrItem.Accum != 0)
                {
                    if (reconArrItem.ReconItem != "" && reconArrItem.ReconItem != "Other" && reconArrItem.ReconItem != "Trash Pickup") // This processes the default GL codes, excluding Trash which doesn't print by default
                    {

                    }
                    //Dim OldComment As Variant
                    //    Dim NewComment As Variant
                    //    OldComment = ThisWorkbook.monthlySheet.Range(Cells(ThisWorkbook.monthlyPrintRow, tmpCol).Address(0, 0)).Comment.Text
                    //    NewComment = OldComment + vbCrLf & reconArray(jCnt, 3) & " (" & reconArray(jCnt, 2) & "): " & FormatCurrency(tmpVal, 2, , vbTrue)
                    // Special adjustment needed for employee trailer sales 0319, which will have already been posted to Employee Sites
                }
                if (reconArrItem.GL == "0319") // SKIP THIS GL Code, already grabbed it above
                { }
            }
            // Now we have to get the deposits held from the Checked In List
            // Open the booking adjustments and checked in list files
            XLWorkbook adjustBook = new XLWorkbook(GenericRoutines.nbfiles.Bookadj);
            IXLWorksheet adjustSheet = adjustBook.Worksheet(1);
            XLWorkbook checkedBook = new XLWorkbook(GenericRoutines.nbfiles.Checkedin);
            IXLWorksheet checkedSheet = checkedBook.Worksheet(1);
            int arrColc, depColc, descColc, idColc, heldColc;
            descColc = 2;
            idColc = 3;
            heldColc = 6;
            arrColc = 7;
            depColc = 8;
            bool validCheckin;
            // If there are no check-ins so do not attempt to process the file (holidays, hurricanes, etc.)
            if (checkedSheet.Row(checkedSheet.LastRowUsed()!.RowNumber()).Cell(1).Value.ToString().IndexOf("No Bookings were found with the specified parameters") == -1)
            {
                for (int iCnt = 2; iCnt <= checkedSheet.LastRowUsed()!.RowNumber(); iCnt++)  // skip the header row
                {
                    double.TryParse(checkedSheet.Row(iCnt).Cell(heldColc).Value.ToString(), out tmpVal); // attempt conversion to double, ignore if false (cellVal will = 0)
                    tmpVal = Math.Round(tmpVal, 2);
                    tmpDesc = checkedSheet.Row(iCnt).Cell(descColc).Value.ToString();
                    tmpID = checkedSheet.Row(iCnt).Cell(idColc).Value.ToString();
                    arrDate = System.DateTime.Parse(System.DateTime.Parse(checkedSheet.Row(iCnt).Cell(arrColc).Value.ToString()).ToShortDateString());
                    departDate = System.DateTime.Parse(System.DateTime.Parse(checkedSheet.Row(iCnt).Cell(depColc).Value.ToString()).ToShortDateString());
                    if ((arrDate - GenericRoutines.repDateTmp).Days == 0 && // were supposed to arrive today and checked in today, this is a good record
                        tmpDesc.IndexOf("Golf Cart Rental") == -1) // except golf carts, which must go through the else block below
                    {
                        validCheckin = true;
                    }
                    else if (tmpDesc == "") // entry for a Split so we ignore it
                    {
                        validCheckin = false;
                    }
                    else // arrival date was earlier so we have to see if this is just a late check in (process it)
                         // or user manipulation of the status field (ignore it)
                    {
                        string bookingIDStr = "Bookings #" + tmpID;
                        int iCnt2 = 2; // the first row is headers
                        while (adjustSheet.Row(iCnt2).Cell(1).Value.ToString() != bookingIDStr &&
                               iCnt2 <= adjustSheet.LastRowUsed()!.RowNumber()) // loop until we find the first matching row
                        {
                            iCnt2++;
                        }
                        validCheckin = true;    // assume it's valid, now test to see if it's not
                        if (iCnt2 <= adjustSheet.LastRowUsed()!.RowNumber()) // no need to process further if we already reached the end of the data
                        {
                            bool ratesQuoted = false;
                            while (adjustSheet.Row(iCnt2).Cell(1).Value.ToString() == bookingIDStr ||
                                  adjustSheet.Row(iCnt2).Cell(1).Value.ToString().IndexOf("Rates Quoted") != -1) // look until there are no more matching rows or we break
                            {
                                if (adjustSheet.Row(iCnt2).Cell(1).Value.ToString().IndexOf("Rates Quoted") != -1 &&
                                   tmpDesc.IndexOf("Golf Cart Rental") != -1)
                                {
                                    ratesQuoted = true;
                                }
                                if (adjustSheet.Row(iCnt2).Cell(2).Value.ToString() == "Arrived When" &&
                                    adjustSheet.Row(iCnt2).Cell(3).Value.ToString() != "")
                                {
                                    DateTime arrWhenDate = System.DateTime.Parse(System.DateTime.Parse(adjustSheet.Row(iCnt2).Cell(3).Value.ToString()).ToShortDateString());
                                    if ((arrWhenDate - GenericRoutines.repDateTmp).Days != 0)
                                    {
                                        validCheckin = false;   // this booking originally checked in on an earlier day so it gets ignored
                                        break;
                                    }
                                }
                                iCnt2++;
                            }
                            if (ratesQuoted && tmpDesc.IndexOf("Golf Cart Rental") != -1)
                            {
                                //validCheckin = false;   // ignore this so same day payments processed as deposits aren't double counted
                                // they will have already been picked up in the Transaction Flow
                            }
                        }
                    }
                    // Retrieve the deposit held if the checkin is valid and the value is non-zero
                    if (validCheckin && tmpVal != 0 )
                    {
                        if ((tmpDesc.IndexOf("Travel Trailer") != -1 || tmpDesc.IndexOf("Villa") != -1 || tmpDesc.IndexOf("Cabin") != -1) && tmpDesc.IndexOf("Storage") == -1)
                        {
                            appliedArray = SupportRoutines.AddApplied(appliedArray, "RentalDepApp", "Booking #" + tmpID + " w/Booked Arrival Date of " + arrDate.ToString("MM/dd/yyyy") + " Deposit Held = $" + tmpVal.ToString(), tmpVal);
                            if ((departDate - arrDate).Days >= 90) //Check for long term unit rental
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "LTUnits", "Booking #" + tmpID + " w/Booked Arrival Date of " + arrDate.ToString("MM/dd/yyyy") + " Deposit Held = $" + tmpVal.ToString(), tmpVal);
                            }
                            else
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "Rental", "Booking #" + tmpID + " w/Booked Arrival Date of " + arrDate.ToString("MM/dd/yyyy") + " Deposit Held = $" + tmpVal.ToString(), tmpVal);
                            }
                        }
                        else if (tmpDesc.IndexOf("WESC") != -1 || tmpDesc.IndexOf("Water") != -1)
                        {
                            appliedArray = SupportRoutines.AddApplied(appliedArray, "SiteDepApp", "Booking #" + tmpID + " w/Booked Arrival Date of " + arrDate.ToString("MM/dd/yyyy") + " Deposit Held = $" + tmpVal.ToString(), tmpVal);
                            if ((departDate - arrDate).Days >= 90) //Check for long term site rental
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "LTSites", "Booking #" + tmpID + " w/Booked Arrival Date of " + arrDate.ToString("MM/dd/yyyy") + " Deposit Held = $" + tmpVal.ToString(), tmpVal);
                            else
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "Campsite", "Booking #" + tmpID + " w/Booked Arrival Date of " + arrDate.ToString("MM/dd/yyyy") + " Deposit Held = $" + tmpVal.ToString(), tmpVal);
                            }
                        }
                        else if (tmpDesc.IndexOf("Golf Cart") != -1) // deduct from deposits and add to revenue
                        {
                            if (tmpVal != 0)
                            {
                                appliedArray = SupportRoutines.AddApplied(appliedArray, "GolfDepApp", "Booking #" + tmpID + " w/Booked Arrival Date of " + arrDate.ToString("MM/dd/yyyy") + " Deposit Held = $" + tmpVal.ToString(), tmpVal);
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "GolfCartRentals", "Booking #" + tmpID + " w/Booked Arrival Date of " + arrDate.ToString("MM/dd/yyyy") + " Deposit Held = $" + tmpVal.ToString(), tmpVal);
                            }
                            else
                            {
                                //appliedArray = SupportRoutines.AddApplied(appliedArray, "GolfDepApp", "Booking #" + tmpID + " w/Booked Arrival Date of " + arrDate.ToString("MM/dd/yyyy") + " Deposit Held = $" + tmpVal2.ToString(), tmpVal2);
                                //revenueArray = SupportRoutines.AddRevenue(revenueArray, "GolfCartRentals", "Booking #" + tmpID + " w/Booked Arrival Date of " + arrDate.ToString("MM/dd/yyyy") + " Deposit Held = $" + tmpVal2.ToString(), tmpVal2);
                            }
                        }
                    }
                    else { }
                    //AddIgnoredNote("Booking " + tmpID + " was ignored (Departed -> Arrived transition).")
                }

            }
            // This next section is for exception income that has to be pulled from the special income spreadsheet
            string repDateShort = GenericRoutines.repDateTmp.ToString("MM/dd/yyyy");
            if (repDateShort == "02/08/2024")
            {
                // Verify that the file exists.  If not there is no point in processing further.
                if (GenericRoutines.AllFilesPresent(6)) 
                { 
                    // define the GL codes that we care about
                    XLWorkbook workBook = new XLWorkbook(GenericRoutines.singleFile.Data);
                    IXLWorksheet workSheet = workBook.Worksheet(1);
                    rowCount = workSheet.LastRowUsed()!.RowNumber();
                    //iterate over the worksheet rows to find the date being checked.
                    for (int i = 2; i <= rowCount; i++) // skip the header row
                    {
                        if (workSheet.Row(i).Cell(1) != null)
                        {
                            System.DateTime tmpDate = workSheet.Row(i).Cell(1).Value; // get date from spreadsheet
                            string tmpDateStr = tmpDate.ToString("MM/dd/yyyy");
                            if (tmpDateStr == repDateShort)
                            {
                                double.TryParse(workSheet.Row(i).Cell(4).Value.ToString(), out double cellVal); // attempt conversion to double, ignore if false (cellVal will = 0)
                                string glStr = "0" + workSheet.Row(i).Cell(2).Value.ToString();
                                if (glStr == "0302")
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, "Annual", workSheet.Row(i).Cell(3).Value.ToString(), cellVal);
                                }
                                else if (glStr == "0358")
                                {
                                    reconArray = SupportRoutines.AddRecon(reconArray, "TransferFees", workSheet.Row(i).Cell(3).Value.ToString(), cellVal);
                                }
                            }
                        }
                    }
                }
            } // end of special exception block
            // loop through the dictionaries to add the parameters for the income items and their values needed for the stored procedure
            // Process revenue array
            foreach (Revenue item in revenueArray)
            {
                //System.Diagnostics.Debug.WriteLine(item.RevType.ToString() + " " + item.Accum.ToString("C"));
                if(item.RevType == "Campsites" || item.RevType == "Rentals" || item.RevType == "Annual" || item.RevType == "LTSites" ||
                    item.RevType == "LTUnits" || item.RevType == "MHPark" || item.RevType == "Storage")
                {
                    SQLSupport.AddSQLParameter(item.RevType, SqlDbType.Money, item.Accum);
                }
                else
                {
                    SQLSupport.AddSQLParameter(item.RevType, SqlDbType.SmallMoney, item.Accum);
                }
            }
            double WescAccum = 0, RentalAccum = 0, GolfAccum = 0;
            int id = 0;
            foreach (Deposits item in depositsArray)
            {
                if (id < 2)
                {
                    WescAccum += item.WescAccum;
                    RentalAccum += item.RentalAccum;
                    GolfAccum += item.GolfAccum;
                }
                else
                {
                    SQLSupport.AddSQLParameter("VouchersPurch", SqlDbType.SmallMoney, item.VouchersAccum);
                    SQLSupport.AddSQLParameter("SiteDepTakenFuture", SqlDbType.SmallMoney, WescAccum);
                    SQLSupport.AddSQLParameter("RentalDepTakenFuture", SqlDbType.SmallMoney, RentalAccum);
                    SQLSupport.AddSQLParameter("GolfDepTakenFuture", SqlDbType.SmallMoney, GolfAccum);
                    SQLSupport.AddSQLParameter("SiteDepTaken", SqlDbType.SmallMoney, item.WescAccum);
                    SQLSupport.AddSQLParameter("RentalDepTaken", SqlDbType.SmallMoney, item.RentalAccum);
                    SQLSupport.AddSQLParameter("GolfDepTaken", SqlDbType.SmallMoney, item.GolfAccum);
                }
                id++;
                //System.Diagnostics.Debug.WriteLine(item.Fy + ":" + item.WescAccum.ToString("C") + " " + item.RentalAccum.ToString("C") + " " + item.GolfAccum.ToString("C"));
            }
            foreach (Applied item in appliedArray)
            {
                if(item.AppliedItem == "SiteDepApp" || item.AppliedItem == "RentalDepApp")
                {
                    SQLSupport.AddSQLParameter(item.AppliedItem, SqlDbType.Money, item.Accum);
                }
                else
                {
                    SQLSupport.AddSQLParameter(item.AppliedItem, SqlDbType.SmallMoney, item.Accum);
                }
                //System.Diagnostics.Debug.WriteLine(item.AppliedItem + " " + item.Accum.ToString("C"));
            }
            foreach (Transfers item in transferArray)
            {
                SQLSupport.AddSQLParameter(item.TranItem, SqlDbType.SmallMoney, item.Accum);
                //System.Diagnostics.Debug.WriteLine(item.TranItem + " " + item.Accum.ToString("C"));
            }
            double MRG1 = 0, MRG2 = 0, MRG3 = 0;
            foreach (Checks item in checkArray)
            {
                SQLSupport.AddSQLParameter(item.CheckItem, SqlDbType.SmallMoney, item.Accum);
                if (item.CheckItem == "CampsitesC" || item.CheckItem == "RentalsC")
                {
                    MRG1 += item.Accum;
                }
                else if (item.CheckItem == "AnnualC" || item.CheckItem == "MHParkC" || 
                         item.CheckItem == "LTCampsitesC" || item.CheckItem == "LTRentalsC")
                {
                    MRG2 += item.Accum;
                }
                else if (item.CheckItem == "StorageC" || item.CheckItem == "OtherC")
                {
                    MRG3 += item.Accum;
                }
                else if (item.CheckItem == "SiteDepositsC")
                {
                    SQLSupport.AddSQLParameter("SiteDepMRG", SqlDbType.SmallMoney, item.Accum);
                }
                else if (item.CheckItem == "RentalDepositsC")
                {
                    SQLSupport.AddSQLParameter("RentalDepMRG", SqlDbType.SmallMoney, item.Accum);
                }
                else if (item.CheckItem == "GolfC")
                {
                    SQLSupport.AddSQLParameter("MRGGolf", SqlDbType.SmallMoney, item.Accum);
                }
                else if (item.CheckItem == "GolfDepositsC")
                {
                    SQLSupport.AddSQLParameter("GolfDepMRG", SqlDbType.SmallMoney, item.Accum);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("This guy fell through: " + item.CheckItem);
                }
                //System.Diagnostics.Debug.WriteLine(item.CheckItem + " " + item.Accum.ToString("C"));
            }
            SQLSupport.AddSQLParameter("MRG1", SqlDbType.SmallMoney, MRG1);
            SQLSupport.AddSQLParameter("MRG2", SqlDbType.SmallMoney, MRG2);
            SQLSupport.AddSQLParameter("MRG3", SqlDbType.SmallMoney, MRG3);
            bool supplementalAdded = false;
            foreach (Recon item in reconArray)
            // NOTE: NEED TO WRITE MISC TRANSACTIONS TO SEPARATE TABLE TOO
            {
                if (item.ReconItem == "Storage" || item.ReconItem == "Misc" || item.ReconItem == "DamageFees" ||
                    item.ReconItem == "LateFees" || item.ReconItem == "LockFees")
                {
                    SQLSupport.AddSQLParameter(item.ReconItem, SqlDbType.SmallMoney, item.Accum, true);
                }
                else if (item.ReconItem == "TransferFees" || item.ReconItem == "Events" || item.ReconItem == "VisitorFees" ||
                         item.ReconItem == "ExtraVehicleFees" || item.ReconItem == "Propane")
                {
                    SQLSupport.AddSQLParameter(item.ReconItem, SqlDbType.SmallMoney, item.Accum);
                }
                else if (item.ReconItem == "Trailer Sales" || item.ReconItem == "Trash Pickup")
                {
                    SQLSupport.AddSQLParameter("Supplemental", SqlDbType.SmallMoney, item.Accum, supplementalAdded);
                    supplementalAdded = true;
                }
                else if (item.MiscTrans == true)
                {
                    SQLSupport.AddSQLParameter("Misc", SqlDbType.SmallMoney, item.Accum, true);
                }
                //System.Diagnostics.Debug.WriteLine(item.ReconItem + " " + item.Accum.ToString("C"));
            }
            //System.Diagnostics.Debug.WriteLine(totOtherCC.ToString("C") + "," + totAmex.ToString("C") + "," + totCash.ToString("C"));
            // add the Supplemental parameter if it was not added in the previous loop
            if (supplementalAdded == false)
            {
                SQLSupport.AddSQLParameter("Supplemental", SqlDbType.SmallMoney, 0);
            }
            // add the parameters needed for the payments table
            SQLSupport.AddSQLParameter("OfficeCC", SqlDbType.Money, totAmex + totOtherCC);
            SQLSupport.AddSQLParameter("OfficeCash", SqlDbType.Money, totCash);
            // act on the transaction table
            string tmpReturned = SQLSupport.ExecuteStoredProcedure(1);
            if (tmpReturned == "SUCCESS")
            {
                string miscParamStr = "";
                // All table changes were completed so we need to add any miscellaneous records to that table
                foreach (Recon item in reconArray)
                {
                    if (item.MiscTrans == true)
                    {
                        miscParamStr += item.GL + '|' + item.ReconItem + "|" + item.Accum.ToString() + '|';
                    }
                }
                // Even if nothing is found we have to process the miscellaneous table in case any previous entries need to be deleted
                SQLSupport.PrepareForImport("UpdateFrontOfficeMiscTable");
                SQLSupport.AddSQLParameterString("ParamString", SqlDbType.NVarChar, miscParamStr);
                // act on the misc table
                _ = SQLSupport.ExecuteStoredProcedure(1);
            }
            SupportRoutines.specialReconArray.Clear(); // This is necessary so no values carry over from date to date
            //visCnt = visCnt;
        } // END OF ReadNewbookFiles
        private static bool SameDayArrival (string bookingIn)
        {
            XLWorkbook localCheckedBook = new XLWorkbook(GenericRoutines.nbfiles.Checkedin);
            IXLWorksheet localCheckedSheet = localCheckedBook.Worksheet(1);
            int idColc = 3;
            for (int iCnt = 2; iCnt <= localCheckedSheet.LastRowUsed()!.RowNumber(); iCnt++)  // skip the header row
            {
                if (localCheckedSheet.Row(iCnt).Cell(idColc).Value.ToString() == bookingIn)
                {
                    int heldColc = 6;
                    if (localCheckedSheet.Row(iCnt).Cell(heldColc).Value.ToString() == "0")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return false;
        } // END OF sameDayArrival
    } // END OF CLASS
} // END OF NAMESPACE