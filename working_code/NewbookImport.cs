
using ClosedXML.Excel;
using GenericSupport;
using MBTP.Logins;
using NewbookSupport;
using SQLStuff;
using System;
using System.Data;
using MBTP.Models;
using MBTP.Interfaces;

// ReservationsService is a class dedicated to Reservations Deposits Table (Daily Breakdown R)
namespace MBTP.Services
{
    public class NewbookImport : NewbookBaseApi
    {
        private readonly IDatabaseConnectionService _dbConnectionService;
        private readonly TransactionFlowApi _transactionFlowApi;
        private readonly CheckedInApi _checkedIn;
        private readonly ReconApi _recon;
        private readonly SupportRoutines _supportRoutines;

        private const double vehicleRateDayTax = 5.6;
        private const double visitorRateBase = 4;
        private const double visitorRateTax = 4.2;
        private const double vehicleRateDayBase = 5;
        private const double vehicleRateYrBase = 40;
        private const double vehicleRateYrTax = 42;
        private const double wristbandRate = 5;

        public NewbookImport(IDatabaseConnectionService dbConnectionService, HttpClient client, ReconApi recon, TransactionFlowApi transactionFlowApi, 
                            CheckedInApi checkedIn, SupportRoutines supportRoutines)
        : base(client)
        {
            _dbConnectionService = dbConnectionService;
            _transactionFlowApi = transactionFlowApi;
            _recon = recon;
            _supportRoutines = supportRoutines;
            _checkedIn = checkedIn;
        }

        public async Task<List<Reservations>> ProcessReservationsAsync(DateTime startDate, DateTime endDate)
        {

            int tmpId;
            string tmpAction, tmpDesc, tmpCat, tmpTrans, tmpClient, tmpGen, flowStr, tmpFTN, tmpTPM, tmpFPM;
            double tmpVal = 0, totAmex = 0, totOtherCC = 0, totCash = 0;
            decimal tmpAmt = 0m;
            System.DateTime arrDate, departDate;
            bool refundChecksActive = false;

            string[] siteCategories = { "WESC", "Water & Electric Only" };
            string[] rentalCategories = { "Ocean Villa", "Cottage", "Cabin", "Travel Trailer" };
            string[] golfCategories = { "Golf" };

            // Call all the APIs needed and retrieve list
            var checkedInList = await _checkedIn.PopulateCheckIns(startDate, endDate);
            var transactionsList = await _transactionFlowApi.PopulateTransactions(startDate, endDate);
            // Call the Reconciliation Api and retrieve the list
            var reconsList = await _recon.PopulateRecons(startDate, endDate);
            var reservations = new Reservations { };
            var reservationsList = new List<Reservations>();
            var paymentsAfterIds = new Dictionary<int, decimal?>();

            // Create the connection to the database and define the SQl command that calls the stored procedure.  Stop here it there's a problem
            SQLSupport sqlSupport = new SQLSupport(_dbConnectionService);
            if (!sqlSupport.PrepareForNewImport("UpdateFrontOfficeTable", startDate))
            {
                return new List<Reservations>();
            }

            /*
            // Verify that all files exist.  If any are missing there is no point in processing further.
            if (!GenericRoutines.AllFilesPresent(1)) 
            {
                return; 
            }
            */

             var depositsTakenList = transactionsList
            .Where(p =>
                p.Category != null &&
                (
                    siteCategories.Any(c => p.Category.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                    rentalCategories.Any(c => p.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
                )
                &&
                !p.Category.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
                (p.Description != null && !p.Description.Contains("Storage", StringComparison.OrdinalIgnoreCase)) &&
                (p.Description.Contains("Deposit", StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains("at Myrtle Beach Travel Park", StringComparison.OrdinalIgnoreCase) ||  
                p.Description.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase)) &&
                p.Deposit != null &&
                p.Deposit.Contains("1", StringComparison.OrdinalIgnoreCase) &&
                (p.Description == null || !p.Description.Contains("EXTRA VEHICLE", StringComparison.OrdinalIgnoreCase))
            )
            .OrderBy(p => p.AccountForId)
            .ToList();

            // Exclude deposits that have had any refunds for the same PaymentTypeReference + AccountForId on the same day
            var depositsTakenList_NoRefunds = IgnoreEntries("Refunds Raised", depositsTakenList, transactionsList);

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
            int tmpMonth = startDate.Month;
            int tmpYear = startDate.Year - (tmpMonth < 10 ? 1 : 0); // Fiscal year starts with month 10
            var depositsArray = new List<NewbookSupport.Deposits>
            {
                new Deposits() { Fy = new DateTime(tmpYear + 2, 10, 1), WescAccum = 0, RentalAccum = 0, GolfAccum = 0, VouchersAccum = 0 },
                new Deposits() { Fy = new DateTime(tmpYear + 1, 10, 1), WescAccum = 0, RentalAccum = 0, GolfAccum = 0, VouchersAccum = 0 },
                new Deposits() { Fy = new DateTime(tmpYear, 10, 1), WescAccum = 0, RentalAccum = 0, GolfAccum = 0, VouchersAccum = 0 }
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
            // The transactions and reconsList are already populated from API calls at the top of the method

            //Spire.Xls.Worksheet specialSheet = SupportRoutines.BuildSpecialReconSheet(reconList);

            int visCnt = 0, vehdCnt = 0, vehaCnt = 0, wristCnt = 0;
            double visTot = 0, vehdTot = 0, vehaTot = 0, wristTot = 0;

            foreach (var t in transactionsList) // Loop through all transactions
            {
               
                tmpId = t.AccountForId;
                tmpAction = t.PaymentTypeAction ?? "";
                tmpCat = t.Category ?? "";
                tmpTrans = t.TransType ?? "";
                tmpFTN = t.FormattedTransNumber ?? "";
                tmpClient = t.ClientAccount ?? "";
                tmpGen = t.GeneratedBy ?? "";
                tmpDesc = t.Description ?? "";
                tmpTPM = t.TranslatedPaymentType ?? "";
                tmpFPM = t.FormattedPaymentMethod ?? "";
                tmpAmt = t.Amount ?? 0m;

                // Replace blank category if we need to (some small cash transactions may remain blank)
                // Do not seek category for vouchers being redeemed as they are tied to a guest not a booking when purchased
                if (string.IsNullOrEmpty(tmpCat) && 
                    (tmpDesc?.IndexOf("Balance Transfer for Gift Voucher to Client Account") ?? -1) == -1)
                {
                    // Get category from transaction - simplified call without Excel parameters
                    tmpCat = SupportRoutines.GetMissingCategory(transactionsList, tmpAction, tmpTrans, tmpClient);
                }
                
                tmpCat = tmpCat.ToUpper();  // Make it uppercase, then simplify for all remaining comparisons
                
                if (!string.IsNullOrEmpty(tmpCat))
                {
                    if (tmpCat != "WESC" && tmpCat != "GUEST" && 
                        (tmpCat.IndexOf("VILLA") != -1 || tmpCat.IndexOf("CABIN") != -1 || 
                        tmpCat.IndexOf("STANDARD") != -1 || tmpCat.StartsWith("ELITE") || 
                        tmpCat.StartsWith("PREMIUM") || tmpCat.IndexOf("COTTAGE") != -1 ||
                        (tmpCat.IndexOf("TRAILER") != -1 && tmpCat.IndexOf("STORAGE") == -1)))
                    {
                        tmpCat = "RENTAL";    // Set an all-encompassing value for any type of rental unit
                    }
                    else if (tmpCat == "WATER & ELECTRIC ONLY")
                    {
                        tmpCat = "WESC";
                    }
                }

                bool isWESC = tmpCat.Contains("WESC");
                bool isRENTAL = tmpCat.Contains("RENTAL");

                // This IF block is necessary to avoid a crash if there are no values in the date fields
                if (!t.ArrivalDate.HasValue || t.ArrivalDate.Value == default(DateTime))
                {
                    arrDate = startDate; // Assign start date. Departure date not needed
                    departDate = startDate;
                }
                else
                {
                    arrDate = t.ArrivalDate.Value.Date;
                    departDate = t.DepartureDate.HasValue ? t.DepartureDate.Value.Date : startDate;
                }
                if (tmpAmt != 0m)
                {
                    tmpVal = 0;
                    tmpVal = Math.Round((double)tmpAmt * -1, 2);
                }

                // flowStr is a common string that will be passed to all "Add" routines
                flowStr = $"{tmpAction} ({tmpGen}) for {tmpCat}/{tmpClient}/{tmpTrans}/{tmpDesc} for {tmpAmt:C}";

                // Since the Transaction Flow is sorted by actions we can open or close the Departing file if we enter or leave
                // the Refund Check entries
                if ((tmpFPM?.IndexOf("Manual Entry Check Refunds") ?? -1) != -1 && !refundChecksActive)
                {
                    refundChecksActive = true;
                }
                
                // 'sheetsToProcess = openSourceFile("Departed List", ThisWorkbook.fixedDownloadsPath, "Bookings_Departing_List_Current_Quarter_" & wrkDay & ".xlsx", "XLSX", "Newbook")
                //  sheetsToProcess = openSourceFile("Departed List", downloadsPath, "Bookings_Departing_List_Current_Quarter_" & wrkDay & ".xlsx", "XLSX", "Newbook")
                //  If sheetsToProcess = -10 Then Exit Sub
                //Set departedBookCurr = ThisWorkbook.srcBook
                //Set departedSheetCurr = ThisWorkbook.srcSheet
                else if (refundChecksActive && (tmpFPM?.IndexOf("Manual Entry Check Refunds") ?? -1) == -1)
                {
                    refundChecksActive = false;
                    //Set departedSheetCurr = Nothing
                    //departedBookCurr.Close False
                    //Set departedBookCurr = Nothing
                }
                if (tmpAction.Contains("Manual Entry") && 
                    (tmpTPM.Contains("Visa") || tmpTPM.Contains("MasterCard") || 
                    tmpTPM.Contains("Discover") || tmpTPM.Contains("AMEX")))
                {
                    string actionString;
                    int paymentsIndex = tmpAction.IndexOf("Payments");
                    int refundsIndex = tmpAction.IndexOf("Refunds");
                    
                    if (paymentsIndex != -1)
                    {
                        actionString = tmpAction.Substring(0, paymentsIndex);
                    }
                    else if (refundsIndex != -1)
                    {
                        actionString = tmpAction.Substring(0, refundsIndex);
                    }
                    else
                    {
                        actionString = tmpAction; // Fallback if neither is found
                    }
                    GenericRoutines.UpdateAlerts2(100, "CRITICAL ERROR", 
                        $"{actionString}({tmpGen}) {tmpTrans} {tmpClient}: {tmpVal:C}", startDate);
                }
                if (tmpFPM.Contains("NONE", StringComparison.OrdinalIgnoreCase) || 
                    tmpFPM.Contains("BARTERCARD", StringComparison.OrdinalIgnoreCase)) 
                { 
                    // Ignore these entries
                }  
                else if (tmpFPM.Contains("Manual Entry Check Refunds"))
                {
                    tmpVal *= -1;
                    if (tmpCat.Contains("ANNUAL"))
                    {
                        SupportRoutines.AddCheck(checkArray, "AnnualC", flowStr, tmpVal);
                    }
                    else if (tmpCat.Contains("MOBILE"))
                    {
                        SupportRoutines.AddCheck(checkArray, "MHParkC", flowStr, tmpVal);
                    }
                    else if (tmpCat.Contains("STORAGE"))
                    {
                        SupportRoutines.AddCheck(checkArray, "StorageC", flowStr, tmpVal);
                    }
                    else if (tmpCat.Contains("WHEELCHAIR") || tmpCat.Trim() == "")
                    {
                        SupportRoutines.AddCheck(checkArray, "OtherC", flowStr, tmpVal);
                    }
                    else
                    {
                        // First check if the booking ever checked in. If not, even if the arrival date is prior to the report
                        // date the refund still comes out of deposits
                        bool bookingCheckedIn = t.HasArrived == true && t.BookingCheckedIn <= t.TransDate;
                        
                        // Booking-specific check added 3/6/25 to overcome procedural error in Newbook 
                        bool isSpecialBooking = t.BookingId == 327777;
                        
                        if (!bookingCheckedIn || isSpecialBooking) // Never arrived or departed so it gets applied to deposits
                        {
                            string checkType = tmpCat.Contains("WESC") ? "SiteDepositsC" : 
                                            tmpCat.Contains("GOLF") ? "GolfDepositsC" : 
                                            "RentalDepositsC";
                            SupportRoutines.AddCheck(checkArray, checkType, flowStr, tmpVal);
                        }
                        else if (tmpCat.Contains("GOLF")) // It's a refund against cart rental income
                        {
                            SupportRoutines.AddCheck(checkArray, "GolfC", flowStr, tmpVal);
                        }
                        else // Check to see if it gets applied to long-term (non-taxable) income or regular income
                        {
                            TimeSpan daysBetween = departDate - arrDate;
                            if (daysBetween.Days >= 90) // Long term rental unit or site
                            {
                                string checkType = tmpCat.Contains("WESC") ? "LTCampsitesC" : "LTRentalsC";
                                SupportRoutines.AddCheck(checkArray, checkType, flowStr, tmpVal);
                            }
                            else
                            {
                                string checkType = tmpCat.Contains("WESC") ? "CampsitesC" : "RentalsC";
                                SupportRoutines.AddCheck(checkArray, checkType, flowStr, tmpVal);
                            }
                        }
                    }
                }
            
                else
                {
                    bool reconMatchFound;
                    // We look for the presence of the record in the reconciliation file. If it exists we will process
                    // it later unless we override it and pull it out based on the IF block below.
                    if (tmpAction.Contains("Refunds") && 
                        !tmpTPM.Contains("Balance Transfer") && 
                        tmpFTN.Contains("Ref #"))
                    {
                        reconMatchFound = SupportRoutines.ValidReconEntryFound(tmpAction, t, tmpVal);
                    }
                    else
                    {
                        reconMatchFound = SupportRoutines.ValidReconEntryFound(tmpAction, t, tmpVal);
                    }
                    
                    if (tmpCat.Contains("STORAGE"))
                    {
                        tmpCat = tmpCat.ToUpper();
                    }
                    tmpDesc = tmpDesc.ToUpper();

                    if (((tmpCat.Contains("WESC", StringComparison.OrdinalIgnoreCase) ||
                        tmpCat.Contains("RENTAL", StringComparison.OrdinalIgnoreCase)) &&
                        ((tmpVal < 0 && !tmpDesc.Contains("ACCOMMODATION", StringComparison.OrdinalIgnoreCase)) ||
                        tmpVal > 0) &&
                        !tmpDesc.Contains("REFUND", StringComparison.OrdinalIgnoreCase) &&
                        Math.Abs(tmpVal) <= 10 * vehicleRateDayTax &&
                        Math.Truncate(tmpVal / (double)vehicleRateDayTax) ==
                        Math.Round(tmpVal / (double)vehicleRateDayTax, 2) &&
                        (!tmpDesc.Contains("DEP", StringComparison.OrdinalIgnoreCase) ||
                        tmpDesc.Contains("EX", StringComparison.OrdinalIgnoreCase)) &&
                        !tmpTPM.Contains("Balance Transfer", StringComparison.OrdinalIgnoreCase)) ||
                        tmpDesc.Contains("VEHICLE REFUND", StringComparison.OrdinalIgnoreCase))
                    {
                        // DebugClassify call removed as it doesn't exist in current codebase
                        vehaTot += tmpVal;
                        reconArray = SupportRoutines.AddRecon(reconArray, "ExtraVehicleFees", flowStr, tmpVal);

                        if (!tmpDesc.Contains("EX", StringComparison.OrdinalIgnoreCase))
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
                            if (tmpTPM.IndexOf("Balance Transfer") != -1) { transferArray = SupportRoutines.AddTransfer(transferArray, "AnnualT", flowStr, tmpVal); }
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
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId; // already non-null
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
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
                        else
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal);
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId; // already non-null
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                        }   // Skip it, we'll grab it in the recon file
                    }
                    // VISITOR FEES BEGINS
                    // VF Section 1
                    else if (tmpDesc.IndexOf("VISITOR") != -1 || tmpDesc.IndexOf("VISTOR") != -1 || tmpDesc.IndexOf("DAY") != -1 ||
                            tmpDesc.IndexOf("PASS") != -1 || tmpDesc.IndexOf("WRIST") != -1 ||
                            (tmpClient == "Cash Account" && tmpDesc == "ACCOMMODATION" &&
                                tmpVal % 2 == 0 && Math.Abs(tmpVal) >= visitorRateBase && tmpTPM.Contains("Balance Transfer")))
                    {
                        if (tmpDesc.IndexOf("WRIST") != -1)
                        {
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            ;
                            wristTot += tmpVal;
                            wristCnt += (int)Math.Truncate(tmpVal / wristbandRate);
                            reconArray = SupportRoutines.AddRecon(reconArray, "VisitorWRIST", flowStr, tmpVal);
                        }
                        else
                        {
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            ;
                            visTot += tmpVal;
                            visCnt += (int)Math.Truncate(tmpVal / visitorRateTax);
                            reconArray = SupportRoutines.AddRecon(reconArray, "VisitorFees", flowStr, tmpVal);
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
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            ;
                            vehaTot += tmpVal;
                            vehaCnt = (int)(vehaCnt + Math.Truncate(tmpVal / vehicleRateYrBase));
                            reconArray = SupportRoutines.AddRecon(reconArray, "VisitorEXYA", flowStr, tmpVal);
                        }
                        else
                        {
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            ;
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
                                !tmpTPM.Contains("Balance Transfer") && Math.Abs(tmpVal) < 200)  // 200 is an arbitrary value
                    {
                        if (Math.Abs(tmpVal) >= vehicleRateYrBase)
                        {
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            ;
                            vehaTot += tmpVal;
                            vehaCnt += (int)Math.Truncate(tmpVal / vehicleRateYrBase);
                            reconArray = SupportRoutines.AddRecon(reconArray, "VisitorEXYA", flowStr, tmpVal);
                            //reconArray = SupportRoutines.AddRecon(reconArray, "Extra Vehicle Fees", flowStr, tmpVal);
                        }
                        else
                        {
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            ;
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
                            tmpTrans.IndexOf("Refunds Raised") == -1 && !tmpTPM.Contains("Balance Transfer") &&
                            Math.Abs(tmpVal) <= visitorRateTax * 7) // 7 is arbitrary value to keep it under lock fee
                    {
                        if (tmpAmt != 0m)
                        {
                            int bookingId = t.AccountForId;
                            decimal amount = tmpAmt;

                            paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                        }
                        ;
                        reconArray = SupportRoutines.AddRecon(reconArray, "VisitorFees", flowStr, tmpVal);
                        visTot += tmpVal;
                        visCnt += (int)Math.Truncate(tmpVal / visitorRateTax);
                    } // VF Section 4 ENDS
                      // VISITOR FEES ENDS
                      // BEACH WHEELCHAIR
                    else if (tmpCat == "BEACH WHEELCHAIR" || tmpDesc.IndexOf("CHAIR") != -1 || tmpDesc.IndexOf("WHEEL") != -1)
                    {
                        revenueArray = SupportRoutines.AddRevenue(revenueArray, "Misc", flowStr, tmpVal);
                        if (tmpAmt != 0m)
                        {
                            int bookingId = t.AccountForId; // already non-null
                            decimal amount = tmpAmt;

                            paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                        }
                    }
                    // TRANSFER FEE
                    else if (tmpDesc.IndexOf("TRANSFER FEE") != -1)
                    {
                        if (reconMatchFound == false) { reconArray = SupportRoutines.AddRecon(reconArray, "TransferFees", flowStr, tmpVal); }
                        else
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal);
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            ;
                        }  // Skip it, we'll grab it in the recon file
                    }
                    // LATE FEE
                    else if (tmpDesc.IndexOf("LATE") != -1)
                    {
                        if (reconMatchFound == false) { reconArray = SupportRoutines.AddRecon(reconArray, "LateFees", flowStr, tmpVal); }
                        else
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal);
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            ;
                        } // Skip it, we'll grab it in the recon file
                    }
                    // DAMAGE FEE
                    else if (tmpDesc.IndexOf("DAMAGE") != -1)
                    {
                        if (reconMatchFound == false) { reconArray = SupportRoutines.AddRecon(reconArray, "DamageFees", flowStr, tmpVal); }
                        else
                        {
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            ;
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal);
                        } // Skip it, we'll grab it in the recon file
                    }
                    // ANNUAL LEASE AND MOBILE HOME
                    else if (tmpCat.IndexOf("ANNUAL") != -1 || tmpCat.IndexOf("MOBILE") != -1)
                    {
                        if (tmpDesc.IndexOf("ANNUAL LEASE") != -1 || tmpDesc == "ACCOMMODATION" ||
                            tmpDesc.IndexOf("MOBILE HOME") != -1 || tmpDesc.IndexOf("BALANCE TRANSFER") != -1)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("ANNUAL") != -1 ? "Annual" : "MHPark", flowStr, tmpVal);
                            if (tmpAction.IndexOf("Balance Transfer") != -1) { transferArray = SupportRoutines.AddTransfer(transferArray, tmpCat.IndexOf("ANNUAL") != -1 ? "AnnualT" : "MHParkT", flowStr, tmpVal); }
                        }
                        else if (Math.Abs(tmpVal) > 150) // Arbitrary value; above this amount will be considered a normal payment
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, tmpCat.IndexOf("ANNUAL") != -1 ? "Annual" : "MHPark", flowStr, tmpVal);
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            SupportRoutines.AddAssumption("Unable to determine intent, assigning to " + (tmpCat.IndexOf("ANNUAL") != -1 ? "Annual Lease" : "Mobile Home") + " from " + flowStr);
                        }
                        else if (tmpDesc.IndexOf("STORAGE") != -1 && reconMatchFound == false) { revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal); }
                        else if (reconMatchFound == false)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Misc", flowStr, tmpVal);
                            if (tmpAmt != 0m)
                            {
                                int bookingId = t.AccountForId;
                                decimal amount = tmpAmt;

                                paymentsAfterIds[bookingId] = paymentsAfterIds.TryGetValue(bookingId, out var existing) ? existing + amount : amount;
                            }
                            GenericRoutines.UpdateAlerts2(1, "Informational", "(1)Unable to determine intent, assigning to Misc from " + flowStr, startDate);
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
                                if (!_supportRoutines.CheckFYChange(tmpId, startDate) == false)
                                {
                                    GenericRoutines.UpdateAlerts2(1, "WARNING", "PRIOR FY! Payment from " + flowStr + " was applied to a past reservation. Current FY used.", startDate);
                                }
                            }
                        }
                        if (tmpAction.IndexOf("Balance Transfer") != -1)
                        {
                            if (t.HasArrived == false)
                            {
                                transferArray = SupportRoutines.AddTransfer(transferArray, "GolfDepositsT", flowStr, tmpVal);
                                depositsArray = SupportRoutines.AddDeposit(depositsArray, "Golf", jCnt, flowStr, tmpVal);
                            }
                            else if (t.HasArrived == true && t.BookingCheckedIn <= t.TransDate)
                            {
                                int returnedVal = _supportRoutines.CheckForCancel(tmpId, startDate);
                                if (returnedVal == 1) // If cancel back out of deposits even if earlier date
                                {
                                    transferArray = SupportRoutines.AddTransfer(transferArray, "GolfDepositsT", flowStr, tmpVal);
                                    depositsArray = SupportRoutines.AddDeposit(depositsArray, "Golf", jCnt, flowStr, tmpVal);
                                }
                                else if (returnedVal == 0)
                                {
                                    transferArray = SupportRoutines.AddTransfer(transferArray, "GolfCarts", flowStr, tmpVal);
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, "GolfCartRentals", flowStr, tmpVal);
                                }
                                else // stop processing. Alert written in CheckForCancel
                                {
                                    return new List<Reservations>();
                                }
                            }
                        }
                        else
                        {
                            // Check if guest arrived same day as transaction
                            bool hasCheckedIn = t.HasArrived == true &&
                                                t.BookingCheckedIn <= t.TransDate;

                            if (hasCheckedIn && !tmpTrans.Contains("REFUND", StringComparison.OrdinalIgnoreCase))
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "GolfCartRentals", flowStr, tmpVal);
                            }
                            depositsArray = SupportRoutines.AddDeposit(depositsArray, "Golf", jCnt, flowStr, tmpVal);
                        }
                    } // GOLF CARTS ENDS

                    // WESC AND RENTALS BEGINS
                    else if (tmpCat.Contains("WESC") || tmpCat.Contains("RENTAL"))
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
                        if (jCnt == 3)    // This indicates a possible error. Put the money in the current FY and report a warning
                        {
                            jCnt = 2;
                            TimeSpan daysBetween = depositsArray[2].Fy - departDate;
                            if (daysBetween.Days < 0) // We can't ignore the error if departure is also in previous FY
                            {
                                if (!_supportRoutines.CheckFYChange(tmpId, startDate) == false)
                                {
                                    GenericRoutines.UpdateAlerts2(1, "WARNING", $"PRIOR FY! Payment from {flowStr} was applied to a past reservation. Current FY used.", startDate);
                                }
                            }
                        }

                        if (tmpTPM.Contains("Balance Transfer"))
                        {
                            if (tmpClient.ToUpper().Contains("FORFEIT"))
                            {
                                if (tmpVal != 30 || tmpVal != 40) // Move from deposits to revenue except for lock fees
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, isWESC ? "Campsites" : "Rentals", flowStr, tmpVal);
                                    transferArray = SupportRoutines.AddTransfer(transferArray, isWESC ? "CampsitesT" : "RentalsT", flowStr, tmpVal);
                                }
                                else
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); // Skip lock fees, we've already claimed them
                                }
                            }
                            else if (tmpDesc.Contains("FOR GIFT VOUCHER FROM CLIENT"))
                            {
                                if (t.HasArrived == false)
                                {
                                    appliedArray = SupportRoutines.AddApplied(appliedArray, tmpCat.IndexOf("WESC") != -1 ? "VouchersRedSiteDep" : "VouchersRedRentalDep", flowStr, tmpVal);
                                    depositsArray = SupportRoutines.AddDeposit(depositsArray, isWESC ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                                    transferArray = SupportRoutines.AddTransfer(transferArray, isWESC ? "SiteDepositsT" : "RentalDepositsT", flowStr, tmpVal);
                                }
                                else if (t.HasArrived == true && t.BookingCheckedIn <= t.TransDate)
                                {
                                    appliedArray = SupportRoutines.AddApplied(appliedArray, tmpCat.IndexOf("WESC") != -1 ? "VouchersRedSite" : "VouchersRedRental", flowStr, tmpVal);
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, isWESC ? "Campsites" : "Rentals", flowStr, tmpVal);
                                    transferArray = SupportRoutines.AddTransfer(transferArray, isWESC ? "CampsitesT" : "RentalsT", flowStr, tmpVal);
                                }
                            }
                            else if (tmpDesc.Contains("BALANCE TRANSFER FROM ACCOUNT") || tmpDesc.Contains("BALANCE TRANSFER TO ACCOUNT") ||
                                    tmpDesc.Contains("BALANCE TRANSFER FROM CLIENT ACCOUNT") || tmpDesc.Contains("BALANCE TRANSFER TO CLIENT ACCOUNT"))
                            {
                                if ((Math.Abs(tmpVal) == 30 && !tmpDesc.Contains("EXCEPTION")) || (Math.Abs(tmpVal) == 30 && !tmpDesc.Contains("EXCEPTION"))) // Assume it's a Lock fee transfer or forfeit so skip it
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); // Lock fee transfer or forfeit so skip it
                                }
                                else if (t.HasArrived == false)
                                {
                                    depositsArray = SupportRoutines.AddDeposit(depositsArray, isWESC ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                                    transferArray = SupportRoutines.AddTransfer(transferArray, isWESC ? "SiteDepositsT" : "RentalDepositsT", flowStr, tmpVal);
                                }
                                else if (t.HasArrived == true && t.BookingCheckedIn <= t.TransDate)
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, isWESC ? "Campsites" : "Rentals", flowStr, tmpVal);
                                    transferArray = SupportRoutines.AddTransfer(transferArray, isWESC ? "CampsitesT" : "RentalsT", flowStr, tmpVal);
                                }
                            }
                            else if (tmpDesc.Contains("CANCEL"))
                            {
                                transferArray = SupportRoutines.AddTransfer(transferArray, isWESC ? "SiteDepositsT" : "RentalDepositsT", flowStr, tmpVal);
                                depositsArray = SupportRoutines.AddDeposit(depositsArray, isWESC ? "WESC" : "Rentals", 2, flowStr, tmpVal);
                            }
                            else if ((tmpTrans.Contains("Voided Payments Voided") && tmpDesc == "") ||
                                    (tmpTrans.Contains("Voided Refunds Voided") && tmpDesc == "") ||
                                    (tmpTrans.Contains("Refunds Raised") && tmpDesc == "") ||
                                    (tmpTrans.Contains("Payments Raised") && tmpDesc == ""))
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, isWESC ? "Campsite" : "Rental", flowStr, tmpVal);
                            }
                            else
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "DROPPED", flowStr, tmpVal); // If we got here we didn't process the balance transfer correctly
                            }
                        }
                        else if ((tmpDesc.Contains("STORAGE") || tmpDesc.Contains("TRAILER MOVE") || tmpDesc == "SERVICE FEE" ||
                                tmpDesc == "MOVING FEE" || tmpDesc.Contains("TOW")) && reconMatchFound == false)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal);
                        }
                        else if (tmpDesc.Contains("EMPLOYEE"))
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Employee", flowStr, tmpVal);
                        }
                        else if (tmpDesc.Contains("DEP") || tmpDesc.Contains("BOOKING") || tmpDesc.Contains("RESTORED CREDIT CARD") ||
                                tmpDesc.Contains("RŽSERVATION") || tmpDesc.Contains("RÉSERVATION") ||
                                tmpDesc.Contains("REFUND") || (tmpDesc.Contains("ACCOMMODATION")))
                        {
                            if ((t.ArrivalDate.HasValue && t.ArrivalDate.Value.Date == t.TransDate.Date) &&
                            t.BookingCheckedIn == null && !tmpTrans.ToUpper().Contains("REFUND") && t.Deposit == "1") // Same day checkin but still treat as a deposit
                            {
                                depositsArray = SupportRoutines.AddDeposit(depositsArray, isWESC ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                            }
                            else if (tmpDesc.Contains("ACCOMMODATION") && !tmpTrans.ToUpper().Contains("REFUND") &&
                            t.HasArrived == true && (t.BookingCheckedIn.HasValue &&
                            t.BookingCheckedIn.Value.Date == t.TransDate.Date))  // Extending existing stay
                            {
                                tmpVal *= -1; // Back it out what we just added for same day so it has no affect on deposits
                                depositsArray = SupportRoutines.AddDeposit(depositsArray, isWESC ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                                tmpVal *= -1; // Restore original value for revenue and CC/cash accumulators
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, isWESC ? "Campsite" : "Rental", flowStr, tmpVal);
                            }
                            else if ((arrDate == t.TransDate.Date && !tmpTrans.ToUpper().Contains("REFUND")) ||
                                    (arrDate < t.TransDate.Date && tmpTrans.ToUpper().Contains("REFUND")))  // Same day checkin, or late checkin that isn't a deposit refund, not a deposit
                            {
                                // Check if actual checkin is before report date                                    
                                if (t.HasArrived == true && t.BookingCheckedIn <= t.TransDate) // If actual checkin is before report date then the refund is from income, otherwise deposits
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, isWESC ? "Campsite" : "Rental", flowStr, tmpVal);
                                }
                                else
                                {
                                    depositsArray = SupportRoutines.AddDeposit(depositsArray, isWESC ? "WESC" : "i", jCnt, flowStr, tmpVal);
                                }
                            }
                            // else if block added 2/5/25 to handle refunds of security deposits
                            else if (t.HasArrived == true && t.BookingCheckedIn <= t.TransDate && tmpTrans.ToUpper().Contains("REFUND") &&
                                    tmpDesc.Contains("DEPOSIT") && tmpVal == -200)
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, isWESC ? "Campsite" : "Rental", flowStr, tmpVal);
                            }
                            else if (t.HasArrived == false)
                            {
                                depositsArray = SupportRoutines.AddDeposit(depositsArray, isWESC ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                            }
                        }
                        else if (tmpDesc.Contains("ACCOMMODATION")) // *****
                        {
                            int returnedVal = _supportRoutines.CheckForCancel(tmpId, startDate);
                            // The IF check will assign any cancellations refunds to deposits and not back them out of income
                            if (tmpTrans.ToUpper().Contains("REFUND") && returnedVal == 1)
                            {
                                depositsArray = SupportRoutines.AddDeposit(depositsArray, isWESC ? "WESC" : "Rentals", jCnt, flowStr, tmpVal);
                            }
                            else
                            {
                                TimeSpan daysBetween = departDate - arrDate;
                                if (daysBetween.Days >= 90)
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, isWESC ? "LTSites" : "LTUnits", flowStr, tmpVal);
                                }
                                else
                                {
                                    revenueArray = SupportRoutines.AddRevenue(revenueArray, isWESC ? "Campsite" : "Rental", flowStr, tmpVal);
                                }
                            }
                        }
                        else if (tmpDesc.Contains("GOLF"))
                        {
                            depositsArray = SupportRoutines.AddDeposit(depositsArray, "Golf", 0, flowStr, tmpVal);
                        }
                        else if (tmpDesc.Contains("VEH"))
                        {
                            reconArray = SupportRoutines.AddRecon(reconArray, "ExtraVehicleFees", flowStr, tmpVal);
                        }
                        else if (reconMatchFound == false)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Misc", flowStr, tmpVal);
                            SupportRoutines.AddAssumption($"Unable to determine intent, assigning to Misc from {flowStr}");
                        }
                        else    // We're skipping this record on purpose, we SHOULD get the money from the recon file
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal);
                        }
                    } // WESC AND RENTALS ENDS 
                    // STORAGE
                    else if (!string.IsNullOrEmpty(tmpCat) && tmpCat != "GUEST" &&
                            (tmpCat.StartsWith("STORAGE") || tmpCat == "FRONT PARKING LOT")) // Check for Storage transactions we don't grab from the recon file
                    {
                        if (reconMatchFound)
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "SKIPPED", flowStr, tmpVal); // We're skipping this record on purpose, we'll get the money from the recon file
                        }
                        else if (tmpTPM.Contains("Balance Transfer"))
                        {
                            transferArray = SupportRoutines.AddTransfer(transferArray, "StorageT", flowStr, tmpVal);
                            if (tmpDesc.Contains("FOR GIFT VOUCHER FROM CLIENT") || tmpDesc.Contains("BALANCE TRANSFER TO ACCOUNT") ||
                                tmpDesc.Contains("BALANCE TRANSFER FROM ACCOUNT") || tmpDesc.Contains("BALANCE TRANSFER TO CLIENT ACCOUNT") ||
                                tmpDesc.Contains("BALANCE TRANSFER FROM CLIENT ACCOUNT"))
                            {
                                revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal);
                                if (tmpDesc.IndexOf("FOR GIFT VOUCHER FROM CLIENT") != -1)
                                {
                                    appliedArray = SupportRoutines.AddApplied(appliedArray, "VouchersRedStorage", flowStr, tmpVal);
                                }
                            }

                            if (tmpDesc.Contains("FOR GIFT VOUCHER TO CLIENT"))
                            {
                                tmpVal *= -1;    // Reverse it arithmetically so it prints correctly on the daily report
                                appliedArray = SupportRoutines.AddApplied(appliedArray, "VouchersRedStorage", flowStr, tmpVal);
                                transferArray = SupportRoutines.AddTransfer(transferArray, "StorageT", flowStr, tmpVal);
                            }
                        }
                        else if (tmpCat == "FRONT PARKING LOT" || tmpDesc.Contains("STOR") || tmpDesc == "MISC STORAGE" ||
                                tmpDesc == "ONLINE PAYMENT" || tmpDesc.Contains("RÉSERVATION") ||
                                tmpDesc.Contains("RESTORED CREDIT CARD") || tmpDesc.Contains("REFUND") ||
                                tmpDesc.StartsWith("BOOKING") || tmpDesc.Contains("MOVE"))
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal);
                        }
                        else if (tmpCat.StartsWith("STORAGE") &&
                                ((tmpDesc.Contains("DEPOSIT") || tmpDesc.Contains("ACCOMMODATION")) ||
                                tmpDesc.Contains("REFUND")))
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Storage", flowStr, tmpVal);
                            SupportRoutines.AddAssumption($"Unable to determine intent, assigning to Storage from {flowStr}");
                        }
                        else
                        {
                            revenueArray = SupportRoutines.AddRevenue(revenueArray, "Misc", flowStr, tmpVal);
                            GenericRoutines.UpdateAlerts2(1, "Informational", $"(3)Unable to determine intent, assigning to Misc from {flowStr}", startDate);
                        }
                    } // STORAGE ENDS
                      // GIFT VOUCHERS BOUGHT
                    else if (string.IsNullOrEmpty(tmpCat) && tmpDesc == "GIFT VOUCHER PAYMENT")
                    {
                        depositsArray = SupportRoutines.AddDeposit(depositsArray, "Vouchers", 2, flowStr, tmpVal);
                    }
                    // GIFT VOUCHERS TRANSFERED TO BE USED
                    else if ((string.IsNullOrEmpty(tmpCat) && tmpDesc.Contains("BALANCE TRANSFER FOR GIFT VOUCHER TO CLIENT ACCOUNT")) ||
                        (tmpCat == "GUEST" && tmpDesc.Contains("BALANCE TRANSFER FOR GIFT VOUCHER FROM CLIENT ACCOUNT")))
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
                        if (string.IsNullOrEmpty(tmpCat) && tmpClient.Contains("GUEST", StringComparison.OrdinalIgnoreCase))
                        {
                            if (tmpDesc.Contains("DEPOSIT"))  // Assume it's going to be a current FY site deposit
                            {
                                depositsArray = SupportRoutines.AddDeposit(depositsArray, "WESC", 2, flowStr, tmpVal);
                            }
                            else if (tmpDesc.Contains("STORAGE"))
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
                    } // END OF ELSE BLOCK
            }
                if (t.PaymentMethod.StartsWith("Authorize.Net"))
                {
                    if (tmpTPM.Contains("AMEX"))
                    {
                        totAmex += tmpVal;
                    }
                    else
                    {
                        totOtherCC += tmpVal;
                    }
                }
                else if (tmpTPM.Contains("Cash") || tmpTPM.Contains("Check"))
                {
                    totCash += tmpVal;
                }
                else if (tmpAction.Contains("Manual Entry") &&
                        (tmpTPM.Contains("Visa") || tmpTPM.Contains("Discover") ||
                        tmpTPM.Contains("MasterCard") || tmpTPM.Contains("AMEX")))
                {
                    GenericRoutines.UpdateAlerts2(1, "CRITICAL ERROR!", flowStr, startDate);
                }
                } // END OF THE TRANSACTION FILE READING LOOP
                // lock fee    
                decimal totalLockFees = GetLockFeesForDay(checkedInList);
                if (totalLockFees > 0)
                {
                    revenueArray = SupportRoutines.AddRevenue(revenueArray, "LockFees", 
                        $"Total Lock Fees for {startDate:MM/dd/yyyy}", (double)totalLockFees);
                }            
                // Look for specific inventory items
                string pymtResult;

                foreach (SpecialRecon item in SupportRoutines.specialReconArray)
                {
                    // look for valid GL code to check, exclude visitor fees, vehicles, golf carts, lost keys
                    if ((item.Gl != "0360" && item.Gl != "0361" && item.Gl is not null && item.Gl.StartsWith("07") == false) || 
                        (item.Gl == "1018" || item.Gl == "1020"))
                    {
                        if (item.Gl == "0362")
                        {
                            item.Gl = item.Gl;
                        }
                        
                        string pymtNum;
                        if (item.Recon_item is not null && item.Recon_item.Contains("Unallocated"))
                        {
                            int startIndex = 9;
                            int endIndex = item.Recon_item.IndexOf(" Unalloc");
                            pymtNum = item.Recon_item.Substring(startIndex, endIndex - startIndex);
                        }
                        else if (item.Desc == "Unallocated Payments" && item.Recon_item is not null)
                        {
                            int hashIndex = item.Recon_item.IndexOf("#");
                            int spaceIndex = item.Recon_item.IndexOf(" ", hashIndex);
                            pymtNum = item.Recon_item.Substring(hashIndex, spaceIndex - hashIndex);
                        }
                        else if (item.Recon_item is not null && item.Recon_item.Contains(" Alloc"))
                        {
                            int startIndex = 9;
                            int endIndex = item.Recon_item.IndexOf(" Alloc");
                            pymtNum = item.Recon_item.Substring(startIndex, endIndex - startIndex);
                        }
                        else
                        {
                            int hashIndex = item.Recon_item!.IndexOf("#");
                            int spaceIndex = item.Recon_item.IndexOf(" ", hashIndex);
                            pymtNum = item.Recon_item.Substring(hashIndex, spaceIndex - hashIndex);
                        }
                        
                        double pymtVal = Math.Round(item.Amount, 2);
                        
                        // Look for matching Payments Raised entry in Transaction Flow using the transactions list
                        pymtResult = SupportRoutines.PaymentRaised(transactionsList, item.Gl, pymtNum, pymtVal * -1, item.Recon_item);
                        
                        if (pymtResult != "NO") // ignore this line if result is "NO"
                        {
                            if (item.Gl == "1018" || item.Gl == "1020") // Correction for original GL codes used in Newbook
                            {
                                item.Gl = "0356";
                            }
                            
                            bool matchFound = false;
                            foreach (Recon reconArrItem in reconArray)
                            {
                                if (string.IsNullOrEmpty(reconArrItem.GL))
                                {
                                    matchFound = true;
                                    break;
                                }
                                else if (item.Gl == reconArrItem.GL)
                                {
                                    matchFound = true;
                                    string reconItem = string.IsNullOrEmpty(reconArrItem.ReconItem) ? reconArrItem.GL : reconArrItem.ReconItem;
                                    reconArray = SupportRoutines.AddRecon(reconArray, reconItem, 
                                        $"{item.Gl} {item.Client} {item.Recon_item} {(item.Amount * -1):C}", item.Amount);
                                    break;
                                }
                            }
                            
                            if (matchFound == false)   // Add this GL code to the recon array list
                            {
                                reconArray.Add(new Recon { ReconItem = item.Recon_item, Accum = item.Amount, GL = item.Gl });
                            }
                        }
                    }
                } // END OF specific inventory items
                // Now we loop through the recon array and process any non-zero values
                foreach (Recon reconArrItem in reconArray)
                {
                    if (reconArrItem.Accum != 0)
                    {
                        if (!string.IsNullOrEmpty(reconArrItem.ReconItem) && 
                            reconArrItem.ReconItem != "Other" && 
                            reconArrItem.ReconItem != "Trash Pickup") // This processes the default GL codes, excluding Trash which doesn't print by default
                        {
                            // Processing logic here (currently empty in original code)
                        }
                        //Dim OldComment As Variant
                        //    Dim NewComment As Variant
                        //    OldComment = ThisWorkbook.monthlySheet.Range(Cells(ThisWorkbook.monthlyPrintRow, tmpCol).Address(0, 0)).Comment.Text
                        //    NewComment = OldComment + vbCrLf & reconArray(jCnt, 3) & " (" & reconArray(jCnt, 2) & "): " & FormatCurrency(tmpVal, 2, , vbTrue)
                        // Special adjustment needed for employee trailer sales 0319, which will have already been posted to Employee Sites
                    }
                    
                    if (reconArrItem.GL == "0319") // SKIP THIS GL Code, already grabbed it above
                    { 
                        // Skip processing
                    }
                }

                // Now we have to get the deposits held for checked in
                if (checkedInList != null && checkedInList.Count > 0)
                {
                    foreach (var record in checkedInList)
                    {
                        decimal paymentsAfterCheckIn = record.PaymentsAfterCheckIn ?? 0m;

                        bool exclude = paymentsAfterIds.Any(p =>
                            p.Key == record.BookingId &&
                            Math.Abs((p.Value ?? 0m) - paymentsAfterCheckIn) < 0.01m
                        );

                        if (exclude)
                            continue;

                        decimal depositsHeld = record.DepositsHeld ?? 0m;
                        decimal securityDeposits = record.SecurityDeposits ?? 0m;
                        decimal onlineBookingFee = record.OnlineBookingFee ?? 0m;
                        decimal cancellationFee = record.CancellationFee ?? 0m;

                        // Subtract security deposits from deposits held
                        decimal netDepositHeld = depositsHeld + paymentsAfterCheckIn + onlineBookingFee + cancellationFee;
                        if(depositsHeld == 0m && securityDeposits > 0m && record.BookingName.Contains("Blocked", StringComparison.OrdinalIgnoreCase))
                        {
                        netDepositHeld = netDepositHeld - securityDeposits;
                        }

                        bool isLongTerm = 
                            (record.BookingDeparture - record.BookingArrival)?.TotalDays >= 90;

                        if (!string.IsNullOrEmpty(record.CategoryName) &&
                            !string.IsNullOrEmpty(record.BookingName) &&
                            rentalCategories.Any(c =>
                                record.CategoryName.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                            !record.CategoryName.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
                            !record.BookingName.Contains("Blocked", StringComparison.OrdinalIgnoreCase))
                        {
                            revenueArray = SupportRoutines.AddRevenue(
                                revenueArray,
                                isLongTerm ? "LTUnits" : "Rental",
                                $"Booking #{record.BookingId} Deposit Held",
                                (double)netDepositHeld
                            );
                        }
                        else if (!string.IsNullOrEmpty(record.CategoryName) &&
                                !string.IsNullOrEmpty(record.BookingName) &&
                                siteCategories.Any(c =>
                                    record.CategoryName.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                                !record.BookingName.Contains("Blocked", StringComparison.OrdinalIgnoreCase))
                        {
                            revenueArray = SupportRoutines.AddRevenue(
                                revenueArray,
                                isLongTerm ? "LTSites" : "Campsite",
                                $"Booking #{record.BookingId} Deposit Held",
                                (double)netDepositHeld
                            );
                        }
                    }
                }

                // Populate applied deposits array using the helper methods
                // Calculate deposits applied from checked-in list
                appliedArray.First(a => a.AppliedItem == "SiteDepApp").Accum = 
                    (double)GetDepositsApplied(siteCategories, checkedInList);
                
                appliedArray.First(a => a.AppliedItem == "RentalDepApp").Accum = 
                    (double)GetDepositsApplied(rentalCategories, checkedInList);

                appliedArray.First(a => a.AppliedItem == "GolfDepApp").Accum =
                    (double)GetDepositsApplied(golfCategories, checkedInList);

                revenueArray.First(a => a.RevType == "GolfCartRentals").Accum += 
                    (double)GetDepositsApplied(golfCategories, checkedInList);


                // loop through the dictionaries to add the parameters for the income items and their values needed for the stored procedure
            // Process revenue array
            foreach (Revenue item in revenueArray)
            {
                //System.Diagnostics.Debug.WriteLine(item.RevType.ToString() + " " + item.Accum.ToString("C"));
                if(item.RevType == "Campsites" || item.RevType == "Rentals" || item.RevType == "Annual" || item.RevType == "LTSites" ||
                    item.RevType == "LTUnits" || item.RevType == "MHPark" || item.RevType == "Storage")
                {
                    sqlSupport.AddSQLParameter(item.RevType, SqlDbType.Money, item.Accum);
                }
                else
                {
                    sqlSupport.AddSQLParameter(item.RevType, SqlDbType.SmallMoney, item.Accum);
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
                    sqlSupport.AddSQLParameter("VouchersPurch", SqlDbType.SmallMoney, item.VouchersAccum);
                    sqlSupport.AddSQLParameter("SiteDepTakenFuture", SqlDbType.SmallMoney, WescAccum);
                    sqlSupport.AddSQLParameter("RentalDepTakenFuture", SqlDbType.SmallMoney, RentalAccum);
                    sqlSupport.AddSQLParameter("GolfDepTakenFuture", SqlDbType.SmallMoney, GolfAccum);
                    sqlSupport.AddSQLParameter("SiteDepTaken", SqlDbType.SmallMoney, item.WescAccum);
                    sqlSupport.AddSQLParameter("RentalDepTaken", SqlDbType.SmallMoney, item.RentalAccum);
                    sqlSupport.AddSQLParameter("GolfDepTaken", SqlDbType.SmallMoney, item.GolfAccum);
                }
                id++;
                //System.Diagnostics.Debug.WriteLine(item.Fy + ":" + item.WescAccum.ToString("C") + " " + item.RentalAccum.ToString("C") + " " + item.GolfAccum.ToString("C"));
            }
            foreach (Applied item in appliedArray)
            {
                if(item.AppliedItem == "SiteDepApp" || item.AppliedItem == "RentalDepApp")
                {
                    sqlSupport.AddSQLParameter(item.AppliedItem, SqlDbType.Money, item.Accum);
                }
                else
                {
                    sqlSupport.AddSQLParameter(item.AppliedItem, SqlDbType.SmallMoney, item.Accum);
                }
                //System.Diagnostics.Debug.WriteLine(item.AppliedItem + " " + item.Accum.ToString("C"));
            }
            foreach (Transfers item in transferArray)
            {
                sqlSupport.AddSQLParameter(item.TranItem, SqlDbType.SmallMoney, item.Accum);
                //System.Diagnostics.Debug.WriteLine(item.TranItem + " " + item.Accum.ToString("C"));
            }
            double MRG1 = 0, MRG2 = 0, MRG3 = 0;
            foreach (Checks item in checkArray)
            {
                sqlSupport.AddSQLParameter(item.CheckItem, SqlDbType.SmallMoney, item.Accum);
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
                    sqlSupport.AddSQLParameter("SiteDepMRG", SqlDbType.SmallMoney, item.Accum);
                }
                else if (item.CheckItem == "RentalDepositsC")
                {
                    sqlSupport.AddSQLParameter("RentalDepMRG", SqlDbType.SmallMoney, item.Accum);
                }
                else if (item.CheckItem == "GolfC")
                {
                    sqlSupport.AddSQLParameter("MRGGolf", SqlDbType.SmallMoney, item.Accum);
                }
                else if (item.CheckItem == "GolfDepositsC")
                {
                    sqlSupport.AddSQLParameter("GolfDepMRG", SqlDbType.SmallMoney, item.Accum);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("This guy fell through: " + item.CheckItem);
                }
                //System.Diagnostics.Debug.WriteLine(item.CheckItem + " " + item.Accum.ToString("C"));
            }
            sqlSupport.AddSQLParameter("MRG1", SqlDbType.SmallMoney, MRG1);
            sqlSupport.AddSQLParameter("MRG2", SqlDbType.SmallMoney, MRG2);
            sqlSupport.AddSQLParameter("MRG3", SqlDbType.SmallMoney, MRG3);
            bool supplementalAdded = false;
            foreach (Recon item in reconArray)
            // NOTE: NEED TO WRITE MISC TRANSACTIONS TO SEPARATE TABLE TOO
            {
                if (item.ReconItem == "Storage" || item.ReconItem == "Misc" || item.ReconItem == "DamageFees" ||
                    item.ReconItem == "LateFees" || item.ReconItem == "LockFees")
                {
                    sqlSupport.AddSQLParameter(item.ReconItem, SqlDbType.SmallMoney, item.Accum, true);
                }
                else if (item.ReconItem == "TransferFees" || item.ReconItem == "Events" || item.ReconItem == "VisitorFees" ||
                         item.ReconItem == "ExtraVehicleFees" || item.ReconItem == "Propane")
                {
                    sqlSupport.AddSQLParameter(item.ReconItem, SqlDbType.SmallMoney, item.Accum);
                }
                else if (item.ReconItem == "Trailer Sales" || item.ReconItem == "Trash Pickup")
                {
                    sqlSupport.AddSQLParameter("Supplemental", SqlDbType.SmallMoney, item.Accum, supplementalAdded);
                    supplementalAdded = true;
                }
                else if (item.MiscTrans == true)
                {
                    sqlSupport.AddSQLParameter("Misc", SqlDbType.SmallMoney, item.Accum, true);
                }
                //System.Diagnostics.Debug.WriteLine(item.ReconItem + " " + item.Accum.ToString("C"));
            }
            if (supplementalAdded == false)
            {
                sqlSupport.AddSQLParameter("Supplemental", SqlDbType.SmallMoney, 0);
            }
            // add the parameters needed for the payments table
            sqlSupport.AddSQLParameter("OfficeCC", SqlDbType.Money, totAmex + totOtherCC);
            sqlSupport.AddSQLParameter("OfficeCash", SqlDbType.Money, totCash);
            // Output miscellaneous records   
            string tmpReturned = sqlSupport.ExecuteStoredProcedure2(1, startDate);
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
                sqlSupport.PrepareForNewImport("UpdateFrontOfficeMiscTable", startDate);
                sqlSupport.AddSQLParameterString("ParamString", SqlDbType.NVarChar, miscParamStr);
                // act on the misc table
                _ = sqlSupport.ExecuteStoredProcedure2(1, startDate);
            }

            Console.WriteLine("\n=== PROCESSING COMPLETE ===");
            SupportRoutines.specialReconArray.Clear();
            reservationsList.Add(reservations);
            return reservationsList;
        }

        private decimal GetLockFeesForDay(List<CheckedIn> checkedInList)
        {
            decimal total = 0m;

            if (checkedInList != null && checkedInList.Count > 0)
            {
                total = checkedInList.Sum(d => d.LockFee ?? 0);
            }
            return total;
        }

        private List<TransactionFlow> IgnoreEntries(string TransType, List<TransactionFlow> listType, List<TransactionFlow> transactions)
        {
            var noVoids = listType.Where(p =>
            {
                if (string.IsNullOrEmpty(p.PaymentTypeReference))
                    return true;

                bool hasRefund = transactions.Any(r =>
                    r.AccountForId == p.AccountForId &&
                    r.PaymentTypeReference == p.PaymentTypeReference &&
                    (r.TransType == TransType ||
                    r.TransType == "Voided Refunds Voided" ||
                    r.TransType == "Voided Payments Voided") &&
                    (r.Amount ?? 0) == Math.Abs(p.Amount ?? 0)
                );

                return !hasRefund;
            }).ToList();

            return noVoids;
        }
        
        // Retrieves deposits applied from checked-in list for specific categories
        private decimal GetDepositsApplied(string[] categories, List<CheckedIn> checkedInList)
        {
            if (checkedInList == null || checkedInList.Count == 0)
                return 0m;
            

            return checkedInList
            .Where(d =>
                !string.IsNullOrEmpty(d.CategoryName) &&
                categories.Any(c =>
                    d.CategoryName.Contains(c, StringComparison.OrdinalIgnoreCase)) &&
                !d.CategoryName.Contains("Storage", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(d.BookingName) &&
                !d.BookingName.Contains("Blocked", StringComparison.OrdinalIgnoreCase)
            )
            .Sum(d => d.DepositsHeld ?? 0m);

            
        }

}}