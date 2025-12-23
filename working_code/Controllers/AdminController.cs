using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MBTP.Retrieval;
using MBTP.Models;
using IronPdf;
using IronPdf.Extensions.Mvc.Core;
using MBTP.Interfaces;
using System.Collections.Generic;
using System.Data;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using MBTP.Pages;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using MBTP.Services;
using System.Globalization;
using MBTP.Logins;
using FinancialC_;
using GenericSupport;
using System.Runtime.CompilerServices;
using ClosedXML.Excel;
using System.IO;



namespace MBTP.Controllers
{

    public class AdminController : Controller
    {
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IConfiguration _configuration;
        private readonly IDatabaseConnectionService _dbConnectionService;
        private readonly AccessLevelsActions _accessLevelsActions;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AdministrationService _adminActions;
        private readonly RetailService _retailService;
        private readonly BlackoutService _blackoutService;
        private readonly BookingApi _bookingAPI;
        private readonly CheckedInApi _checkedInApi;
        private readonly ReconApi _reconApi;
        private readonly TransactionFlowApi _transactionFlowApi;
        private readonly DailyReport _dailyReport;
        private readonly OccupancyApi _occupancyApi;
        private readonly PaymentsApi _paymentsApi;
        private readonly ChargesApi _chargesApi;
        private readonly OccupancyRepo _occupancyRepo;
        private readonly ReconRepo _reconRepo;
        private readonly TransactionFlowRepo _transactionFlowRepo;
        private readonly CheckedInListRepo _checkedInListRepo;
        private readonly BookingsRepo _bookingsRepo;
        private readonly InventoryApi _inventory;
        private readonly GLAccounts _glAccounts;
        private readonly NewbookImport _newbook;
        
        public AdminController(
            ILogger<HomeController> logger,
            IConfiguration configuration,
            IDatabaseConnectionService dbConnectionService,
            ICompositeViewEngine viewEngine,
            AccessLevelsActions accessLevelsActions,
            BookingApi bookingAPI,
            IHttpContextAccessor httpContextAccessor,
            AdministrationService adminActions,
            RetailService retailService,
            BlackoutService blackoutService,
            CheckedInApi checkedInApi,
            DailyReport dailyReport,
            ReconApi reconApi,
            TransactionFlowApi transactionFlowApi,
            OccupancyApi occupancyApi,
            PaymentsApi paymentsApi,
            ChargesApi chargesApi,
            OccupancyRepo occupancyRepo,
            ReconRepo reconRepo,
            TransactionFlowRepo transactionFlowRepo,
            CheckedInListRepo checkedInListRepo,
            BookingsRepo bookingsRepo,
            InventoryApi inventory,
            GLAccounts glAccounts,
            NewbookImport newbook
        )

        {
            _viewEngine = viewEngine;
            _configuration = configuration;
            _dbConnectionService = dbConnectionService;
            _accessLevelsActions = accessLevelsActions;
            _bookingAPI = bookingAPI;
            _httpContextAccessor = httpContextAccessor;
            _adminActions = adminActions;
            _retailService = retailService;
            _blackoutService = blackoutService;
            _checkedInApi = checkedInApi;
            _dailyReport = dailyReport;
            _reconApi = reconApi;
            _transactionFlowApi = transactionFlowApi;
            _occupancyApi = occupancyApi;
            _paymentsApi = paymentsApi;
            _chargesApi = chargesApi;
            _occupancyRepo = occupancyRepo;
            _reconRepo = reconRepo;
            _transactionFlowRepo = transactionFlowRepo;
            _checkedInListRepo = checkedInListRepo;
            _bookingsRepo = bookingsRepo;
            _inventory = inventory;
            _glAccounts = glAccounts;
            _newbook = newbook;
        }
        

        [Authorize]
        public IActionResult Reports()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult ManageUsers()
        {
            DataSet AccessLevels = _accessLevelsActions.RetrieveAccessLevels();
            return View(AccessLevels);
        }
        [Authorize]
        public async Task<IActionResult> ProcessExports(
            string startDate,
            string endDate,
            string opts
        )

        {
            string host = _httpContextAccessor.HttpContext?.Request?.Host.Value ?? "unknown";

            if (startDate is not null)
            {
                DateTime startDateParsed, endDateParsed;
                if (!DateTime.TryParse(startDate, out startDateParsed))
                {
                    // Fallback to setting both dates to yesterday's date if the parsing of the start date fails
                    startDateParsed = DateTime.Today.AddDays(-1);
                    endDateParsed = startDateParsed;
                }
                else if (!DateTime.TryParse(endDate, out endDateParsed))
                {
                    // Fallback to setting end date to same as start date if the parsing of the end date fails
                    endDateParsed = startDateParsed;
                }
                bool cnvrtResult;
                for (
                    DateTime counter = startDateParsed;
                    counter <= endDateParsed;
                    counter = counter.AddDays(1)
                )
                {
                    GenericRoutines.repDateStr = counter.ToString("yyyy-MM-dd");
                    cnvrtResult = System.DateTime.TryParse(GenericRoutines.repDateStr, out GenericRoutines.repDateTmp);
                    /*
                    if (opts.Contains('F'))
                    {
                        NewbookImport newbookImport = new NewbookImport(_dbConnectionService);
                        newbookImport.ReadNewbookFiles();
                    }
                    */
                    if (opts.Contains('F'))
                    {
                        await _newbook.ProcessReservationsAsync(startDateParsed, endDateParsed);
                    }
                    if (opts.Contains('A'))
                    {
                        POSImports posImports = new POSImports(_dbConnectionService);
                        posImports.ReadArcadeFiles();
                    }
                    if (opts.Contains('C'))
                    {
                        POSImports posImports = new POSImports(_dbConnectionService);
                        posImports.ReadCoffeeFiles();
                    }
                    if (opts.Contains('K'))
                    {
                        POSImports posImports = new POSImports(_dbConnectionService);
                        posImports.ReadKayakFiles();
                    }
                    if (opts.Contains('G') && counter < new DateTime(2025, 9, 18)) // last date for guest services is 9/17/2025
                    {
                        POSImports posImports = new POSImports(_dbConnectionService);
                        posImports.ReadGuestFiles();
                    }
                    //                    if (opts.Contains('M')) { POSImports.ReadSpecialAddonsFile(); }
                    if (opts.Contains('S'))
                    {
                        await _retailService.PopulateRetailData("Store", counter);
                    }
                }
            }
            ViewBag.Host = host;
            return View();
        }
        /*
        [Authorize]
        public async Task<IActionResult> PopulateBookings(DateTime? month)
        {
            var selectedMonth = month ?? DateTime.Today;
            ViewBag.SelectedMonth = selectedMonth;
            var periodFrom = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            var periodTo = periodFrom.AddMonths(1);
            if (month is not null)
            {
                var bookings = await _bookingAPI.PopulateBookings(periodFrom, periodTo);
                await _bookingsRepo.SaveBookingsAsync(bookings);
            }
            return View();
        }
        */
        [Authorize]
        public async Task<IActionResult> PopulateBookings(DateTime? day)
        {
            if (day == null)
            {
                ViewBag.SelectedDay = null;
                return View();
            }

            var selectedDay = day.Value;
            ViewBag.SelectedDay = selectedDay;

            // Process reservations and deposits
            var bookings = await _bookingAPI.PopulateBookings(selectedDay, selectedDay.AddDays(1).AddTicks(-1));
            await _bookingsRepo.SaveBookingsAsync(bookings);

            return View();
        } 
        
        [Authorize]
        public async Task<IActionResult> PopulateCheckIns(DateTime? day)
        {
            if (day == null)
            {
                ViewBag.SelectedDay = null;
                return View();
            }

            var selectedDay = day.Value;
            ViewBag.SelectedDay = selectedDay;
            var checkedInList = await _checkedInApi.PopulateCheckIns(selectedDay, selectedDay.AddDays(1).AddTicks(-1));
            await _checkedInListRepo.SaveCheckedInListAsync(checkedInList);
            var reportData = await _dailyReport.RetrieveCheckInsReport(selectedDay, selectedDay.AddDays(1));

            ViewBag.TitleDate = selectedDay.ToString("MMMM dd, yyyy");
            return View(reportData);
        }
        
        /*
         [Authorize]
        public async Task<IActionResult> PopulateCheckIns(DateTime? day)
        {
            if (day == null)
            {
                ViewBag.SelectedDay = null;
                return View();
            }

            var startDay = day.Value.Date;
            var today = DateTime.Today;

            // Store daily resultsz
            var allReports = new List<(DateTime Date, DataSet Report)>();

            for (var currentDay = startDay; currentDay <= today; currentDay = currentDay.AddDays(1))
            {
                var from = currentDay;
                var to = currentDay.AddDays(1).AddTicks(-1);

                // Your existing populate/save
                var checkedInList = await _checkedInApi.PopulateCheckIns(from, to);
                await _checkedInListRepo.SaveCheckedInListAsync(checkedInList);

                // Get daily report
                DataSet ds = await _dailyReport.RetrieveCheckInsReport(from, currentDay.AddDays(1));

                // Store a tuple of (Date, DataSet)
                allReports.Add((currentDay, ds));
            }

            ViewBag.TitleDate = $"{startDay:MMMM dd, yyyy} - {today:MMMM dd, yyyy}";

            return View(allReports);
        }
*/

        [Authorize]
        public async Task<IActionResult> PopulateRecons(DateTime? day)
        {
            if (day == null)
            {
                ViewBag.SelectedDay = null;
                return View();
            }

            var selectedDay = day.Value;
            ViewBag.SelectedDay = selectedDay;
            var reconReport = await _reconApi.PopulateRecons(selectedDay, selectedDay.AddDays(1).AddTicks(-1));
            await _reconRepo.SaveReconAsync(reconReport);

            ViewBag.TitleDate = selectedDay.ToString("MMMM dd, yyyy");
            return View();


        }

        [Authorize]
        public async Task<IActionResult> PopulateOccupancy(DateTime? day)
        {
            if (day == null)
            {
                ViewBag.SelectedDay = null;
                return View();
            }

            var selectedDay = day ?? DateTime.Today;
            ViewBag.SelectedDay = selectedDay;
            var occupancyList = await _occupancyApi.PopulateOccupancy(selectedDay, selectedDay.AddDays(1).AddTicks(-1));
            var reportData = await _dailyReport.RetrieveOccupancyReport(selectedDay, selectedDay.AddDays(1));
            await _occupancyRepo.SaveOccupancyAsync(occupancyList, selectedDay);

            ViewBag.TitleDate = selectedDay.ToString("MMMM dd, yyyy");
            return View(reportData);

        }

        [Authorize]
        public async Task<IActionResult> PopulateTransactions(DateTime? day)
        {
            if (day == null)
            {
                ViewBag.SelectedDay = null;
                return View();
            }

            var selectedDay = day ?? DateTime.Today;
            ViewBag.SelectedDay = selectedDay;
            var transactionFlow = await _transactionFlowApi.PopulateTransactions(selectedDay, selectedDay.AddDays(1).AddTicks(-1));
            await _transactionFlowRepo.SaveTransactionFlowsAsync(transactionFlow);

            ViewBag.TitleDate = selectedDay.ToString("MMMM dd, yyyy");

            return View();
        }

        [Authorize]
        public async Task<IActionResult> PopulatePayments(DateTime? day)
        {
            if (day == null)
            {
                ViewBag.SelectedDay = null;
                return View();
            }

            var selectedDay = day ?? DateTime.Today;
            ViewBag.SelectedDay = selectedDay;
            await _paymentsApi.PopulatePayments(selectedDay, selectedDay.AddDays(1).AddTicks(-1));

            ViewBag.TitleDate = selectedDay.ToString("MMMM dd, yyyy");
            return View();

        }

        [Authorize]
        public async Task<IActionResult> PopulateCharges(DateTime? day)
        {
            if (day == null)
            {
                ViewBag.SelectedDay = null;
                return View();
            }

            var selectedDay = day ?? DateTime.Today;
            ViewBag.SelectedDay = selectedDay;
            await _chargesApi.PopulateCharges(selectedDay, selectedDay.AddDays(1).AddTicks(-1));

            ViewBag.TitleDate = selectedDay.ToString("MMMM dd, yyyy");

            return View();
        }
   
        
        [HttpPost]
        public async Task<string> AddUpdateUser(int lidIn, string unameIn, string fnameIn, string lnameIn, string pwdIn, int accIDIn)
        {
            string addResult = await _accessLevelsActions.AddUpdateUser(lidIn, unameIn, fnameIn, lnameIn, pwdIn, accIDIn);
            return addResult;
        }
        [HttpPost]
        public async Task<string> DeleteUser(int LIDIn)
        {
            string deleteResult = await _accessLevelsActions.DeleteUser(LIDIn);
            return deleteResult;
        }

        [Authorize]
        public IActionResult ReviewDistinctAlerts()
        {
            DataSet ActiveAlerts = _adminActions.ReviewDistinctAlerts();

            var reasons = new List<SelectListItem>
            {
                new SelectListItem { Value = "Holiday", Text = "Holiday" },
                new SelectListItem { Value = "Maintenance", Text = "Maintenance" },
                new SelectListItem { Value = "Occupancy", Text = "Occupancy" },
                new SelectListItem { Value = "Seasonal", Text = "Seasonal" },
                new SelectListItem { Value = "Staffing", Text = "Staffing" },
                new SelectListItem { Value = "Weather", Text = "Weather" },
                new SelectListItem { Value = "Other", Text = "Other" }
            };

            ViewBag.Reasons = reasons;

            return View(ActiveAlerts);
        }


        public IActionResult BlackoutDates()
        {
            var data = _blackoutService.ViewAllBlackoutDates();
            var operations = _blackoutService.GetAllProfitCenters();

            ViewBag.ProfitCenters = operations.Select(loc => new SelectListItem
            {
                Value = loc.PCID.ToString(),
                Text = loc.Description
            }).ToList();

            return View(data);
        }
        

        [HttpPost]
        [Route("Admin/AddBlackout")]
        public IActionResult AddBlackout(BlackoutDate blackout)
        {
            try
            {
                if (blackout == null)
                {
                    TempData["ErrorMessage"] = "No blackout data received.";
                    return RedirectToAction("BlackoutDates");
                }

                if (blackout.PCID <= 0)
                {
                    TempData["ErrorMessage"] = "Please select a valid location.";
                    return RedirectToAction("BlackoutDates");
                }

                if (blackout.StartDate == default(DateTime) || blackout.EndDate == default(DateTime))
                {
                    TempData["ErrorMessage"] = "Please provide valid start and end dates.";
                    return RedirectToAction("BlackoutDates");
                }

                if (string.IsNullOrWhiteSpace(blackout.Reason))
                {
                    TempData["ErrorMessage"] = "Please provide a reason for the blackout.";
                    return RedirectToAction("BlackoutDates");
                }

                // Additional validation
                if (blackout.StartDate.Date > blackout.EndDate.Date)
                {
                    TempData["ErrorMessage"] = "Start date cannot be after end date.";
                    return RedirectToAction("BlackoutDates");
                }

                // Check for overlaps
                if (_blackoutService.HasOverlap(blackout.PCID, blackout.StartDate.Date, blackout.EndDate.Date))
                {
                    TempData["ErrorMessage"] = "This blackout period overlaps with an existing blackout for this location.";
                    return RedirectToAction("BlackoutDates");
                }

                blackout.StartDate = blackout.StartDate.Date;
                blackout.EndDate = blackout.EndDate.Date;

                // Add the blackout
                _blackoutService.InsertBlackoutDate(blackout);

                var duration = (blackout.EndDate - blackout.StartDate).Days + 1;
                TempData["SuccessMessage"] = $"Blackout date added successfully for {duration} day{(duration == 1 ? "" : "s")}.";

                return RedirectToAction("BlackoutDates");
            }
            catch (ArgumentException ex)
            {
                System.Diagnostics.Debug.WriteLine($"ArgumentException in AddBlackout: {ex.Message}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("BlackoutDates");
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"InvalidOperationException in AddBlackout: {ex.Message}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("BlackoutDates");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error in AddBlackout: {ex.Message}\nStackTrace: {ex.StackTrace}");
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                return RedirectToAction("BlackoutDates");
            }
        }

        [HttpPost]
        [Route("Admin/EditBlackout")]
        public IActionResult EditBlackout([FromBody] BlackoutDate blackout)
        {
            try
            {
                if (_blackoutService.HasOverlap(blackout.PCID, blackout.StartDate, blackout.EndDate, blackout.BlackoutID))
                {
                    return Conflict(new { success = false, message = "This blackout overlaps with an existing entry." });
                }

                _blackoutService.UpdateBlackoutDate(blackout);
                return Ok(new { success = true, message = "Blackout updated successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "An unexpected error occurred while updating the blackout." });
            }
        }

        [HttpGet]
        [Route("Admin/IsBlackout")]
        public IActionResult IsBlackout(int PCID, DateTime date)
        {
            bool result = _blackoutService.IsBlackout(PCID, date);
            return Ok(new
            {
                PCID,
                date = date.ToString("yyyy-MM-dd"),
                isBlackout = result,
            });
        }

        [HttpPost]
        public IActionResult DeleteBlackout(int id)
        {

            _blackoutService.DeleteBlackoutDate(id);
            return RedirectToAction("BlackoutDates");

        }

        //For ReviewDistinctAlerts


        [HttpPost]
        public IActionResult AddBlackoutFromAlert([FromBody] AddBlackoutRequest req)
        {

            var start = req.TransDate.Date;
            var end = req.TransDate.Date;

            if (_blackoutService.HasOverlap(req.PCID, start, end))
            {
                return Conflict(new { success = false, message = "A blackout already exists for this date." });
            }

            var blackout = new BlackoutDate
            {
                PCID = req.PCID,
                StartDate = start,
                EndDate = end,
                Reason = req.Reason
            };

            _blackoutService.InsertBlackoutDate(blackout);

            return Ok(new { sucess = true, message = "Blackout added." });
        }

        public async Task<IActionResult> ExportCheckInsToExcel(DateTime? day)
        {
            var selectedDay = day ?? DateTime.Today;
            DataSet ds = await _dailyReport.RetrieveCheckInsReport(selectedDay, selectedDay.AddDays(1));

            if (ds == null || ds.Tables.Count == 0)
            {
                return Content("No data available");
            }

            DataTable dt = ds.Tables[0];

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Checked In");
                worksheet.Cell(1, 1).InsertTable(dt);
                worksheet.Columns().AdjustToContents();

                worksheet.Protect("readonly");

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();

                    string fileName = $"Checked_In_List_{selectedDay:MMMdd}.xlsx";
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }
        public async Task<IActionResult> ExportOccupancyToExcel(DateTime? day)
        {
            var selectedDay = day ?? DateTime.Today;
            DataSet ds = await _dailyReport.RetrieveOccupancyReport(selectedDay, selectedDay.AddDays(1));

            if (ds == null || ds.Tables.Count == 0)
            {
                return Content("No data available");
            }

            DataTable dt = ds.Tables[0];

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Occupancy");
                worksheet.Cell(1, 1).InsertTable(dt);
                worksheet.Columns().AdjustToContents();

                worksheet.Protect("readonly");

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();

                    string fileName = $"Occupancy_Report_{selectedDay:MMMdd}.xlsx";
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }

        }

        public IActionResult ExportTransactionFlowToExcel(DateTime? day)
        {
            var selectedDay = day ?? DateTime.Today;
            DataSet ds = new DataSet();

            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand(@"dbo.RetrieveTransactionFlowReport", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@StartDate", selectedDay);
                    cmd.Parameters.AddWithValue("@EndDate", selectedDay.AddDays(1).AddTicks(-1));

                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    da.Fill(ds);
                    sqlConn.Close();

                    DataTable dt = ds.Tables[0];

                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Transaction Flow");
                        worksheet.Cell(1, 1).InsertTable(dt);
                        worksheet.Columns().AdjustToContents();

                        worksheet.Protect("readonly");

                        using (var stream = new MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            var content = stream.ToArray();

                            string fileName = $"Transaction_Flow_{selectedDay:MMMdd}.xlsx";
                            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving recon report: " + ex.Message);
                throw;
            }
        }
        
        public IActionResult ExportReconsToExcel(DateTime? day)
        {
            var selectedDay = day ?? DateTime.Today;
            DataSet ds = new DataSet();
    
            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand(@"dbo.RetrieveReconReport", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@StartDate", selectedDay);
                    cmd.Parameters.AddWithValue("@EndDate", selectedDay.AddDays(1).AddTicks(-1));

                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    da.Fill(ds);
                    sqlConn.Close();

                    DataTable dt = ds.Tables[0];

                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Reconciliation");
                        worksheet.Cell(1, 1).InsertTable(dt);
                        worksheet.Columns().AdjustToContents();

                        worksheet.Protect("readonly");

                        using (var stream = new MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            var content = stream.ToArray();

                            string fileName = $"Reconciliation_Report_{selectedDay:MMMdd}.xlsx";
                            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving recon report: " + ex.Message);
                throw;
            }
        }
    }
}