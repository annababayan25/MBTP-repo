using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MBTP.Retrieval;
using MBTP.Models;
using MBTP.Converter;
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
        private readonly BookingAPI _bookingAPI;
        private readonly CheckedInApi _checkedInApi;
        private readonly ReconApi _reconApi;
        private readonly TransactionFlowApi _transactionFlowApi;
        private readonly DailyReport _dailyReport;


        public AdminController(
            ILogger<HomeController> logger,
            IConfiguration configuration,
            IDatabaseConnectionService dbConnectionService,
            ICompositeViewEngine viewEngine,
            AccessLevelsActions accessLevelsActions,
            BookingAPI bookingAPI,
            IHttpContextAccessor httpContextAccessor,
            AdministrationService adminActions,
            RetailService retailService,
            BlackoutService blackoutService,
            CheckedInApi checkedInApi,
            DailyReport dailyReport,
            ReconApi reconApi,
            TransactionFlowApi transactionFlowApi
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
                    if (opts.Contains('F'))
                    {
                        NewbookImport newbookImport = new NewbookImport(_dbConnectionService);
                        newbookImport.ReadNewbookFiles();
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
        [Authorize]
        public async Task<IActionResult> PopulateBookings(DateTime? month)
        {
            var selectedMonth = month ?? DateTime.Today;
            ViewBag.SelectedMonth = selectedMonth;
            var periodFrom = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            var periodTo = periodFrom.AddMonths(1);
            if (month is not null)
            {
                await _bookingAPI.PopulateBookings(periodFrom, periodTo);
            }
            return View();
        }

        [Authorize]
        public async Task<IActionResult> PopulateCheckIns(DateTime? day)
        {
            var selectedDay = day ?? DateTime.Today;
            await _checkedInApi.PopulateCheckIns(selectedDay, selectedDay.AddDays(1));
            var reportData = await _dailyReport.RetrieveCheckedInReport(selectedDay, selectedDay.AddDays(1));

            if (reportData == null || reportData.Tables.Count == 0 || reportData.Tables[0].Rows.Count == 0)
            {
                var yesterday = DateTime.Today.AddDays(-1);
                await _checkedInApi.PopulateCheckIns(yesterday, yesterday.AddDays(1));
                reportData = await _dailyReport.RetrieveCheckedInReport(yesterday, yesterday.AddDays(1));
                ViewBag.SelectedDay = yesterday;
            }
            else
            {
                ViewBag.SelectedDay = selectedDay;
            }

            ViewBag.TitleDate = ViewBag.SelectedDay.ToString("MMMM dd, yyyy");
            return View(reportData);
        }

        [Authorize]
        public async Task<IActionResult> PopulateRecons(DateTime? day)
        {
            var selectedDay = day ?? DateTime.Today;
            ViewBag.SelectedDay = selectedDay;

            // One full day range
            var periodFrom = selectedDay.Date; // midnight
            var periodTo = selectedDay.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999

            if (day is not null)
            {
                await _reconApi.PopulateRecons(periodFrom, periodTo);
            }

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




    }
}