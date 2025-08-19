using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MBTP.Retrieval;
using MBTP.Models;
using MBTP.Converter;
using MBTP.Interfaces;
using IronPdf;
using IronPdf.Extensions.Mvc.Core;
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

    public class HomeController : Controller
    {
        private readonly WeatherService _weatherService = new WeatherService();
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IConfiguration _configuration;
        private readonly IDatabaseConnectionService _dbConnectionService;
        private readonly OccupancyService _occupancyService;
        private readonly DailyService _dailyService;
        private readonly DailyBookingsService _dailyBookingsService;
        private readonly LoginClass _loginClass;
        private readonly NewBookService _newBookService;
        private readonly BookingRepository _bookingRepository;
        private readonly TrailerMovesReport _trailerMovesReport;
        private readonly ExpressCheckinsReport _expressCheckinsReport;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IDatabaseConnectionService dbConnectionService, ICompositeViewEngine viewEngine, WeatherService weatherService,
                                OccupancyService occupancyService, DailyService dailyService, DailyBookingsService dailyBookingsService,
                                NewBookService newBookService, BookingRepository bookingRepository, TrailerMovesReport trailermovesReport,
                                ExpressCheckinsReport expressCheckinsReport)
        {
            _viewEngine = viewEngine;
            _configuration = configuration;
            _dbConnectionService = dbConnectionService;
            _weatherService = weatherService;
            _occupancyService = occupancyService;
            _dailyService = dailyService;
            _dailyBookingsService = dailyBookingsService;
            _loginClass = new LoginClass(_dbConnectionService);
            _newBookService = newBookService;
            _bookingRepository = bookingRepository;
            _trailerMovesReport = trailermovesReport;
            _expressCheckinsReport = expressCheckinsReport;

        }
        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult FDB()
        {
            return View();
        }
        public IActionResult Newbook()
        {
            return View();
        }
        public IActionResult Construction()
        {
            return View();
        }

        public async Task<IActionResult> DailyBookings(DateTime? month)
        {
            var selectedMonth = month ?? DateTime.Today;
            ViewBag.SelectedMonth = selectedMonth;

            var periodFrom = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            var periodTo = periodFrom.AddMonths(1).AddDays(-1);

            DataSet bookingsByDay = _dailyBookingsService.GetBookingsDataset(periodFrom, periodTo);

            return View(bookingsByDay);
        }
        public async Task<IActionResult> Occupancy(DateTime? month)
        {
            try
            {
                Console.WriteLine("Starting Occupancy action method");
                var selectedMonth = month ?? DateTime.Now;
                var occupancyReport = await _occupancyService.GetOccupancyReportAsync(selectedMonth);

                ViewBag.SelectedMonth = selectedMonth;
                ViewBag.IncludeAges = false;

                Console.WriteLine("Occupancy action completed successfully");
                return View(occupancyReport);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Occupancy action: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while retrieving the occupancy report.";
                return View(new DataSet());
            }
        }

        public async Task<IActionResult> OccupancyAges(DateTime? month)
        {
            try
            {
                Console.WriteLine("Starting OccupancyAges action method");
                var selectedMonth = month ?? DateTime.Now;
                var occupancyReport = await _occupancyService.GetOccupancyReportAsync(selectedMonth);

                ViewBag.SelectedMonth = selectedMonth;
                ViewBag.AvailableMonths = GetAvailableMonths();
                ViewBag.IncludeAges = true;

                Console.WriteLine("Occupancy action completed successfully");
                return View("~/Views/Home/Occupancy.cshtml", occupancyReport);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Occupancy action: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while retrieving the occupancy report.";
                return View(new FilteredOccupancyReport
                {
                    Month = month ?? DateTime.Now,
                    DailyOccupancies = new List<DailyOccupancy>()
                });
            }
        }

        private List<DateTime> GetAvailableMonths()
        {
            var months = new List<DateTime>();
            for (int i = 0; i < 12; i++)
            {
                months.Add(new DateTime(DateTime.Now.Year, DateTime.Now.AddMonths(-i).Month, 1));
            }
            return months;
        }
        [HttpGet]
        public IActionResult GetBookingCountsByStateYearMonth(string state, int year, int month)
        {
            //Console.WriteLine($"Request received for state: {state}, year: {year}, month: {month}");
            var bookingDataSet = _bookingRepository.GetBookingCountsByStateYearMonth(state, year, month);
            int bookingCount = bookingDataSet.Tables[0].Rows.Count > 0 ? Convert.ToInt32(bookingDataSet.Tables[0].Rows[0]["BookingCount"]) : 0;

            //Console.WriteLine($"Booking count for state: {state}, year: {year}, month: {month} is {bookingCount}");
            return Json(new { count = bookingCount });
        }
        public IActionResult Administration()
        {
            return View();
        }
        [Authorize]
        public IActionResult BookingsDem()
        {
            return View();
        }
        public IActionResult Cameras()
        {
            return View();
        }
        public IActionResult TrailerMoves(string moveDate)
        {
            DateTime selectedDate;
            if (!DateTime.TryParse(moveDate, out selectedDate))
            {
                selectedDate = DateTime.Today.AddDays(1);
            }
            DataSet TrailerMovesByDay = _trailerMovesReport.RetrieveTrailerMovesData(selectedDate);
            ViewBag.reportDate = selectedDate;
            return View(TrailerMovesByDay);
        }
        public IActionResult ExpressCheckins(string checkinDate)
        {
            DateTime selectedDate;
            if (!DateTime.TryParse(checkinDate, out selectedDate))
            {
                selectedDate = DateTime.Today;
            }
            DataSet ExpressCheckinsByDay = _expressCheckinsReport.RetrieveExpressCheckinsData(selectedDate);
            ViewBag.reportDate = selectedDate;
            return View(ExpressCheckinsByDay);
        }
        [Authorize]
        public async Task<IActionResult> GenerateTrailerPDF(string moveDate)
        {
            DateTime selectedDate;
            if (!DateTime.TryParse(moveDate, out selectedDate))
            {
                selectedDate = DateTime.Today.AddDays(1);
            }
            DataSet TrailerMovesByDay = _trailerMovesReport.RetrieveTrailerMovesData(selectedDate);
            ViewBag.reportDate = selectedDate;
            string htmlContent = RenderViewToString("TrailerMoves", TrailerMovesByDay, true);

            ChromePdfRenderer Renderer = new ChromePdfRenderer();
            Renderer.RenderingOptions.CustomCssUrl = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "css", "site.css");
            Renderer.RenderingOptions.PrintHtmlBackgrounds = true;
            Renderer.RenderingOptions.PaperSize = IronPdf.Rendering.PdfPaperSize.Letter;
            Renderer.RenderingOptions.PaperOrientation = IronPdf.Rendering.PdfPaperOrientation.Portrait;
            Renderer.RenderingOptions.ForcePaperSize = true;
            Renderer.RenderingOptions.MarginTop = 10;
            Renderer.RenderingOptions.MarginLeft = 5;
            Renderer.RenderingOptions.MarginRight = 5;
            Renderer.RenderingOptions.MarginBottom = 10;
            Renderer.RenderingOptions.PaperFit.UseResponsiveCssRendering(1024);

            var PDF = Renderer.RenderHtmlAsPdf(htmlContent);
            string outputPath = Path.Combine(Path.GetTempPath(), "TrailerMoves.pdf");
            PDF.SaveAs(outputPath);

            byte[] fileBytes = System.IO.File.ReadAllBytes(outputPath);
            return File(fileBytes, "application/pdf", $"TrailerMoves_{selectedDate.ToString("yyyy-MM-dd")}.pdf");
        }
        public async Task<IActionResult> GenerateExpressPDF(string checkinDate)
        {
            DateTime selectedDate;
            if (!DateTime.TryParse(checkinDate, out selectedDate))
            {
                selectedDate = DateTime.Today;
            }
            DataSet ExpressCheckinsByDay = _expressCheckinsReport.RetrieveExpressCheckinsData(selectedDate);
            ViewBag.reportDate = selectedDate;
            string htmlContent = RenderViewToString("ExpressCheckins", ExpressCheckinsByDay, true);

            ChromePdfRenderer Renderer = new ChromePdfRenderer();
            Renderer.RenderingOptions.CustomCssUrl = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "css", "site.css");
            Renderer.RenderingOptions.PrintHtmlBackgrounds = true;
            Renderer.RenderingOptions.PaperSize = IronPdf.Rendering.PdfPaperSize.Letter;
            Renderer.RenderingOptions.PaperOrientation = IronPdf.Rendering.PdfPaperOrientation.Portrait;
            Renderer.RenderingOptions.ForcePaperSize = true;
            Renderer.RenderingOptions.MarginTop = 10;
            Renderer.RenderingOptions.MarginLeft = 5;
            Renderer.RenderingOptions.MarginRight = 5;
            Renderer.RenderingOptions.MarginBottom = 10;
            Renderer.RenderingOptions.PaperFit.UseResponsiveCssRendering(1024);

            var PDF = Renderer.RenderHtmlAsPdf(htmlContent);
            string outputPath = Path.Combine(Path.GetTempPath(), "ExpressCheckins.pdf");
            PDF.SaveAs(outputPath);

            byte[] fileBytes = System.IO.File.ReadAllBytes(outputPath);
            return File(fileBytes, "application/pdf", $"ExpressCheckins_{selectedDate.ToString("yyyy-MM-dd")}.pdf");
        }

        [Authorize]
        private string RenderViewToString(string viewName, object model, bool isPdf)
        {
            ViewData.Model = model;
            ViewData["IsPdf"] = isPdf;
            using (var writer = new StringWriter())
            {
                var viewResult = _viewEngine.FindView(ControllerContext, viewName, false);
                if (viewResult.View == null)
                {
                    throw new ArgumentNullException($"{viewName} does not match any available view");
                }
                var viewContext = new ViewContext(ControllerContext, viewResult.View, ViewData, TempData, writer, new HtmlHelperOptions());
                viewResult.View.RenderAsync(viewContext);

                if (isPdf)
                {
                    string cssLink = "<link rel=\"stylesheet\" href=\"/css/site.css\" type=\"text/css\" />";
                    writer.GetStringBuilder().Insert(0, cssLink);
                }

                return writer.GetStringBuilder().ToString();
            }
        }
            public IActionResult Index()
        {
            Dashboard report = new Dashboard(_dbConnectionService);
            DataSet dashData = report.RetrieveDashboardData();

            if (dashData.Tables.Contains("Alerts"))
            {
                var alertsTable = dashData.Tables["Alerts"];

                DataTable filteredAlerts = alertsTable.Clone();

                foreach (DataRow row in alertsTable.Rows)
                {
                    string alertText = row["AlertText"]?.ToString() ?? "";

                    if (!alertText.Contains("blackout", StringComparison.OrdinalIgnoreCase))
                    {
                        filteredAlerts.ImportRow(row);
                    }
                }

                dashData.Tables.Remove("Alerts");
                filteredAlerts.TableName = "Alerts";
                dashData.Tables.Add(filteredAlerts);
            }

            return View(dashData);
        }


        private LoginModel GetLoginModel()
        {
            string accIDValue = User.FindFirst("AccID")?.Value;
            if (!int.TryParse(accIDValue, out int accID))
            {
                return null; // Handle the case where AccID is not valid or not present
            }

            var model = new LoginModel
            {
                Tables = new List<DataTable>(),
                AccID = accID
            };

            using (var sqlConn = _dbConnectionService.CreateConnection())
            {
                sqlConn.Open();

                // Example query to fetch tables based on AccID
                SqlCommand command = new SqlCommand("SELECT * FROM LoginsHope WHERE AccID = @AccID", sqlConn);
                command.Parameters.Add(new SqlParameter("@AccID", SqlDbType.Int) { Value = accID });

                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataSet dataSet = new DataSet();
                adapter.Fill(dataSet);

                foreach (DataTable table in dataSet.Tables)
                {
                    model.Tables.Add(table);
                }
            }

            return model;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = "")
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string returnUrl = null)
        {

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Username and password cannot be empty.";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            bool isAuthenticated = _loginClass.ValidateLogin(username, password, out string LID, out string accID);

            if (isAuthenticated)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim("AccID", accID),
                    new Claim("LID", LID),
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
                //Default environment is Live
                HttpContext.Session.SetString("EnvironmentMode", "Live");

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                if (accID == "1")
                {
                    HttpContext.Session.SetString("ShowEnvModal", "true");
                    return RedirectToAction("Index", "Home");
                }

                if (accID == "6")
                {
                    return RedirectToAction("FDB", "Home");
                }

                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "Invalid username or password";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
        [HttpPost]
        public async Task<JsonResult> UpdateClaims(string bookingIdIn)
        {
            ExpressCheckinsClaimed expressClaims = new ExpressCheckinsClaimed
            {
                BookingId = int.Parse(bookingIdIn.Replace("claimBox", "")),
                ClaimedDate = DateTime.Now,
                DateString = DateTime.Now.ToShortTimeString()
            };
            bool updateResult = await _expressCheckinsReport.PostClaimedExpress(expressClaims);
            return Json(expressClaims);
        }


        [HttpPost]
        public IActionResult ToggleSqlConnection()
        {
            var currentMode = HttpContext.Session.GetString("EnvironmentMode") ?? "Live";
            var newMode = (currentMode == "Test") ? "Live" : "Test";

            HttpContext.Session.SetString("EnvironmentMode", newMode);

            return Json(new { success = true, mode = newMode });
        }

        public string GetCurrentConnection()
        {
            var currStringObject = HttpContext.Session.GetString("sqlConnString");
            if (currStringObject != null)
            {
                return (string)currStringObject;
            }
            else
            {
                return "oops";
            }
        }

        [HttpGet]
        public IActionResult GetCurrentEnvironmentMode()
        {
            var mode = HttpContext.Session.GetString("EnvironmentMode") ?? "Live";
            return Json(new { mode });
        }
        
        [HttpGet]
         public IActionResult CheckDatabase()
        {
             var mode = HttpContext.Session.GetString("EnvironmentMode") ?? "Live";
             var connName = mode == "Test" ? "TestConnection" : "DefaultConnection";
             var connString = _configuration.GetConnectionString(connName);
             return Content($"Mode: {mode} \nConnection String: {connString}");
        }

    }
        
       



        /*
        public IActionResult ChooseEnvironment()
        {
            return View();
        }

        [HttpGet]
        [Authorize]
        public IActionResult EnvironmentMode()
        {
            // If user tries to GET the page directly, just send them to the ChooseEnvironment screen
            return RedirectToAction("ChooseEnvironment", "Home");
        }


        [HttpPost]
        [Authorize]
        public IActionResult EnvironmentMode(string mode)
        {
            if (mode != "Live" && mode != "Test")
            {
                mode = "Live";
            }

            HttpContext.Session.SetString("EnvironmentMode", mode);

            // Redirect to Home/Index after setting mode
            return RedirectToAction("Index", "Home");
        }


       
        /*
        [HttpPost]
        public async Task<IActionResult> SetInitialConnection()
        {
            var currStringObject = HttpContext.Session.GetString("sqlConnString");
            if (currStringObject == null || currStringObject.ToString().Contains("Daily"))
            {
                HttpContext.Session.SetString("sqlConnString", "Daily Report");
                return Json(true);
            }
            else
            {
                return Json(false);
            }
        }
        public string GetCurrentConnection()
        {
            var currStringObject = HttpContext.Session.GetString("sqlConnString");
            if (currStringObject != null)
            {
                return (string)currStringObject;
            }
            else
            {
                return "oops";
            }
        }
        */
    }

