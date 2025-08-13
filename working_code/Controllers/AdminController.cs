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

        public AdminController(
            ILogger<HomeController> logger,
            IConfiguration configuration,
            IDatabaseConnectionService dbConnectionService,
            ICompositeViewEngine viewEngine,
            AccessLevelsActions accessLevelsActions,
            NewBookService newBookService,
            IHttpContextAccessor httpContextAccessor,
            AdministrationService adminActions,
            RetailService retailService
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
        [HttpPost]
        public async Task<JsonResult> AddBlackout(string blackoutIdIn)
        {
            bool updateResult = await _adminActions.PostBlackoutDate(blackoutIdIn);
            return Json(blackoutIdIn);
        }
        
        
    }
}

