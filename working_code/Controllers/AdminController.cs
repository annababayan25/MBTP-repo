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
using System;
using Spire.Xls;

namespace MBTP.Controllers
{

    public class AdminController : Controller
    {
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IConfiguration _configuration;
        private readonly IDatabaseConnectionService _dbConnectionService;
        private readonly AccessLevelsActions _accessLevelsActions;
        private readonly NewBookService _newBookService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AdministrationService _adminActions;
        private readonly RetailService _retailService;
        private readonly BlackoutService _blackoutService;

        public AdminController(
            ILogger<HomeController> logger,
            IConfiguration configuration,
            IDatabaseConnectionService dbConnectionService,
            ICompositeViewEngine viewEngine,
            AccessLevelsActions accessLevelsActions,
            NewBookService newBookService,
            IHttpContextAccessor httpContextAccessor,
            AdministrationService adminActions,
            RetailService retailService,
            BlackoutService blackoutService
        )
        {
            _viewEngine = viewEngine;
            _configuration = configuration;
            _dbConnectionService = dbConnectionService;
            _accessLevelsActions = accessLevelsActions;
            _newBookService = newBookService;
            _httpContextAccessor = httpContextAccessor;
            _adminActions = adminActions;
            _retailService = retailService;
            _blackoutService = blackoutService;
        }
        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult ManageUsers()
        {
            DataSet AccessLevels = _accessLevelsActions.RetrieveAccessLevels();
            Console.WriteLine(HttpContext.Session.GetString("sqlConnString"));
            return View(AccessLevels);
        }
        [Authorize]
        public async Task<IActionResult> ProcessExports(
            string startDate,
            string endDate,
            string opts
        )
        {
            string host = _httpContextAccessor.HttpContext.Request.Host.Value;

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
                    var useTestDb = opts.Contains('T');
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
                    if (opts.Contains('G'))
                    {
                        POSImports posImports = new POSImports(_dbConnectionService);
                        posImports.ReadGuestFiles();
                    }
                    //                    if (opts.Contains('M')) { POSImports.ReadSpecialAddonsFile(); }
                    if (opts.Contains('S'))
                    {
                        await RetailService.PopulateRetailData(counter);
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
                await _newBookService.PopulateBookings(periodFrom, periodTo);
            }
            return View();
        }
        [HttpPost]
        public async Task<string> AddNewUser(string unameIn, string pwdIn, int accIDIn)
        {
            string addResult = await _accessLevelsActions.AddNewUser(unameIn, pwdIn, accIDIn);
            return addResult;
        }
        [Authorize]
        public IActionResult ReviewDistinctAlerts()
        {
            DataSet ActiveAlerts = _adminActions.ReviewDistinctAlerts();
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

    }
}

