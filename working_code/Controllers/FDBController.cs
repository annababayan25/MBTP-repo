using Microsoft.AspNetCore.Mvc;
using MBTP.Retrieval;
using System.Data;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Authorization;
using MBTP.Services;
using GenericSupport;
using MBTP.Interfaces;

namespace MBTP.Controllers
{

    public class FDBController : Controller
    {
        private readonly ICompositeViewEngine _viewEngine;
        private readonly IDatabaseConnectionService _dbConnectionService;
        private readonly DailyReport _dailyReport;
        private readonly AdministrationService _adminActions;
        private readonly SpecialAddonsService _specialAddonsService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public FDBController(ILogger<HomeController> logger, IConfiguration configuration, IDatabaseConnectionService dbConnectionService, ICompositeViewEngine viewEngine,
                              DailyReport dailyReport, AdministrationService adminActions, SpecialAddonsService specialAddonsService,
                              IHttpContextAccessor httpContextAccessor)
        {
            _viewEngine = viewEngine;
            _dbConnectionService = dbConnectionService;
            _dailyReport = new DailyReport(_dbConnectionService);
            _adminActions = adminActions;
            _specialAddonsService = specialAddonsService;
            _httpContextAccessor = httpContextAccessor;
        }
    
        [Authorize]
        public async Task<IActionResult> Daily(string date)
        {
            DateTime selectedDate;
            if (!DateTime.TryParse(date, out selectedDate))
            {
                // Fallback to yesterday's date if the parsing fails
                selectedDate = DateTime.Today.AddDays(-1);
            }

                var dailyReport = new DailyReport(_dbConnectionService);
                DataSet reportData = await dailyReport.RetrieveData(selectedDate);
                DateTime finalDate = selectedDate;

                // Assign the weather condition and report data to ViewBag
                ViewBag.FinalDate = finalDate;
                
                //ViewBag.WasItRaining = wasItRaining ? 1 : 0;
                //ViewBag.Temperature = temperature;
                //ViewBag.ReportData = reportData;

                return View(reportData);
        }

        [Authorize]
        public async Task<IActionResult> DailyReservation(string date)
        {
            DateTime selectedDate;

            if (!DateTime.TryParse(date, out selectedDate))
            {
                selectedDate = DateTime.Today.AddDays(-1);
            }

            //RetrievalReport report = new RetrievalReport(_configuration);
            var retrievalReport = new RetrievalReport(_dbConnectionService);
            DataSet dataSetter = await retrievalReport.RetrievalOfData(selectedDate);
            DateTime finalDate = selectedDate;
            ViewBag.FinalDate = finalDate;
            return View(dataSetter);
        }
        [Authorize] 
        public IActionResult Monthly(string whichMonth)
        {
            MonthlyReport report = new MonthlyReport(_dbConnectionService);
            DateTime startDate, endDate;
            if (whichMonth == null || whichMonth == "Current")
            {
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                endDate = DateTime.Today.AddDays(-1);
                //endDate = startDate.AddMonths(1).AddDays(-1);
            }
            else
            {
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1);
                endDate = startDate.AddMonths(1).AddDays(-1);
            }

            bool isPositive;
            decimal currentMonthTotal;
            DataSet dataSetted = report.GetMonthly(startDate, endDate, out isPositive, out currentMonthTotal);
            
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.IsPositive = isPositive;
            ViewBag.CurrentMonthTotal = currentMonthTotal;
            ViewBag.WhichMonth = whichMonth;
            return View(dataSetted);
        }
        [Authorize]
        public IActionResult Yearly()
{
    YearlyReport report = new YearlyReport(_dbConnectionService);

    // Pass null for startDate and endDate to let the method calculate dynamically
    bool isPositive;
    decimal currentYearTotal;
    DataSet dataSets = report.GetYearly(null, null, out isPositive, out currentYearTotal);

    // Extract the dynamically calculated start and end dates for display purposes
    DateTime now = DateTime.Now;
    DateTime startDate = (now.Month >= 10)
        ? new DateTime(now.Year, 10, 1) // Current fiscal year's October
        : new DateTime(now.Year - 1, 10, 1); // Last fiscal year's October
    DateTime endDate = now; // End at today's date

    // Pass calculated values to the view
    ViewBag.StartDate = startDate;
    ViewBag.EndDate = endDate;
    ViewBag.IsPositive = isPositive;
    ViewBag.CurrentYearTotal = currentYearTotal;
    return View(dataSets);
}
        [Authorize] 
        public async Task<IActionResult> GeneratePDF(string date)
        {
            DateTime selectedDate;
            if (!DateTime.TryParse(date, out selectedDate))
            {
                selectedDate = DateTime.Today.AddDays(-1);
            }
            var dailyReport = new DailyReport(_dbConnectionService);
            DataSet dataSetter = await dailyReport.RetrieveData(selectedDate);
            DateTime finalDate = selectedDate;
            ViewBag.FinalDate = finalDate;
            string htmlContent = RenderViewToString("Daily", dataSetter, true);

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
            string outputPath = Path.Combine(Path.GetTempPath(), "DailyReport.pdf");
            PDF.SaveAs(outputPath);

            byte[] fileBytes = System.IO.File.ReadAllBytes(outputPath);
            return File(fileBytes, "application/pdf", $"DailyReport-Other_{finalDate.ToString("yyyy-MM-dd")}.pdf");
        }
        [Authorize] 
        public async Task<IActionResult> GeneratePDF2(string date)
        {
            DateTime selectedDate;
            if (!DateTime.TryParse(date, out selectedDate))
            {
                selectedDate = DateTime.Today.AddDays(-1);
            }
            var retrievalReport = new RetrievalReport(_dbConnectionService);
            DataSet dataSetter = await retrievalReport.RetrievalOfData(selectedDate);
            DateTime finalDate = selectedDate;
            ViewBag.FinalDate = finalDate;
            string htmlContent = RenderViewToString("DailyReservation", dataSetter, true);

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
            string outputPath = Path.Combine(Path.GetTempPath(), "DailyReservationReport.pdf");
            PDF.SaveAs(outputPath);

            byte[] fileBytes = System.IO.File.ReadAllBytes(outputPath);
            return File(fileBytes, "application/pdf", $"DailyReport-Office_{finalDate.ToString("yyyy-MM-dd")}.pdf");

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
        public IActionResult DailyBreakdownF(DateTime ? date, string whichMonth)
    {
        DateTime effectiveDate = date ?? DateTime.Now;

        YearlyReport report = new YearlyReport(_dbConnectionService);
        DateTime fiscalYearStartDate = (effectiveDate.Month >= 10)
            ? new DateTime(effectiveDate.Year, 10, 1)  // Current fiscal year's October
            : new DateTime(effectiveDate.Year - 1, 10, 1); // Last fiscal year's October

        DataSet data = report.GetDailyBreakdownData(effectiveDate, fiscalYearStartDate);

        ViewBag.date = effectiveDate;
        ViewBag.fiscalYearStartDate = fiscalYearStartDate;
        ViewBag.WhichMonth = whichMonth;
        return View(data);
    }
        [Authorize] 
        public IActionResult DailyBreakdownR(DateTime ? date, string whichMonth)
    {
        DateTime effectiveDate = date ?? DateTime.Now;

        YearlyReport report = new YearlyReport(_dbConnectionService);
        DateTime fiscalYearStartDate = (effectiveDate.Month >= 10)
            ? new DateTime(effectiveDate.Year, 10, 1)  // Current fiscal year's October
            : new DateTime(effectiveDate.Year - 1, 10, 1); // Last fiscal year's October

        DataSet data = report.GetDailyBreakdownData(effectiveDate, fiscalYearStartDate);

        ViewBag.date = effectiveDate;
        ViewBag.fiscalYearStartDate = fiscalYearStartDate;
        ViewBag.WhichMonth = whichMonth;
        return View(data);
    }
        [Authorize] 
        public IActionResult DailyBreakdownA(DateTime ? date, string whichMonth)
    {
        DateTime effectiveDate = date ?? DateTime.Now;

        YearlyReport report = new YearlyReport(_dbConnectionService);
        DateTime fiscalYearStartDate = (effectiveDate.Month >= 10)
            ? new DateTime(effectiveDate.Year, 10, 1)  // Current fiscal year's October
            : new DateTime(effectiveDate.Year - 1, 10, 1); // Last fiscal year's October

        DataSet data = report.GetDailyBreakdownData(effectiveDate, fiscalYearStartDate);

        ViewBag.date = effectiveDate;
        ViewBag.fiscalYearStartDate = fiscalYearStartDate;
        ViewBag.WhichMonth = whichMonth;
        return View(data);
    }
        [Authorize] 
        public IActionResult DailyBreakdownC(DateTime ? date, string whichMonth)
    {
        DateTime effectiveDate = date ?? DateTime.Now;

        YearlyReport report = new YearlyReport(_dbConnectionService);
        DateTime fiscalYearStartDate = (effectiveDate.Month >= 10)
            ? new DateTime(effectiveDate.Year, 10, 1)  // Current fiscal year's October
            : new DateTime(effectiveDate.Year - 1, 10, 1); // Last fiscal year's October

        DataSet data = report.GetDailyBreakdownData(effectiveDate, fiscalYearStartDate);

        ViewBag.date = effectiveDate;
        ViewBag.fiscalYearStartDate = fiscalYearStartDate;
        ViewBag.WhichMonth = whichMonth;
        return View(data);
    }
        [Authorize] 
        public IActionResult DailyBreakdownG(DateTime ? date, string whichMonth)
    {
         DateTime effectiveDate = date ?? DateTime.Now;

        YearlyReport report = new YearlyReport(_dbConnectionService);
        DateTime fiscalYearStartDate = (effectiveDate.Month >= 10)
            ? new DateTime(effectiveDate.Year, 10, 1)  // Current fiscal year's October
            : new DateTime(effectiveDate.Year - 1, 10, 1); // Last fiscal year's October

        DataSet data = report.GetDailyBreakdownData(effectiveDate, fiscalYearStartDate);

        ViewBag.date = effectiveDate;
        ViewBag.fiscalYearStartDate = fiscalYearStartDate;
        ViewBag.WhichMonth = whichMonth;
        return View(data);
    }
        [Authorize] 
        public IActionResult DailyBreakdownK(DateTime  ? date, string whichMonth)
    {
         DateTime effectiveDate = date ?? DateTime.Now;

        YearlyReport report = new YearlyReport(_dbConnectionService);
        DateTime fiscalYearStartDate = (effectiveDate.Month >= 10)
            ? new DateTime(effectiveDate.Year, 10, 1)  // Current fiscal year's October
            : new DateTime(effectiveDate.Year - 1, 10, 1); // Last fiscal year's October

        DataSet data = report.GetDailyBreakdownData(effectiveDate, fiscalYearStartDate);

        ViewBag.date = effectiveDate;
        ViewBag.fiscalYearStartDate = fiscalYearStartDate;
        ViewBag.WhichMonth = whichMonth;
        return View(data);
    }
        [Authorize] 
        public IActionResult DailyBreakdownM(DateTime ? date, string whichMonth)
    {
        DateTime effectiveDate = date ?? DateTime.Now;

        YearlyReport report = new YearlyReport(_dbConnectionService);
        DateTime fiscalYearStartDate = (effectiveDate.Month >= 10)
            ? new DateTime(effectiveDate.Year, 10, 1)  // Current fiscal year's October
            : new DateTime(effectiveDate.Year - 1, 10, 1); // Last fiscal year's October

        DataSet data = report.GetDailyBreakdownData(effectiveDate, fiscalYearStartDate);

        ViewBag.date = effectiveDate;
        ViewBag.fiscalYearStartDate = fiscalYearStartDate;
        ViewBag.WhichMonth = whichMonth;
        return View(data);
    }
        [Authorize] 
        public IActionResult MonthlyBreakdownF()
        {
            // Define the start of the fiscal year. Adjust this date according to your fiscal year start.
                DateTime fiscalYearStartDate = new DateTime(DateTime.Now.Year-1, 10, 1);
                

                YearlyReport report = new YearlyReport(_dbConnectionService);
                DataSet data = report.GetMonthlyBreakdownData(fiscalYearStartDate);

                ViewBag.FiscalYearStartDate = fiscalYearStartDate;
                return View(data);
        }
        [Authorize]
        public IActionResult MonthlyBreakdownA()
        {
            // Define the start of the fiscal year. Adjust this date according to your fiscal year start.
            DateTime fiscalYearStartDate = new DateTime(DateTime.Now.Year - 1, 10, 1);


            YearlyReport report = new YearlyReport(_dbConnectionService);
            DataSet data = report.GetMonthlyBreakdownData(fiscalYearStartDate);

            ViewBag.FiscalYearStartDate = fiscalYearStartDate;
            return View(data);
        }
        [Authorize] 
        public IActionResult MonthlyBreakdownR()
        {
                // Define the start of the fiscal year. Adjust this date according to your fiscal year start.
                DateTime fiscalYearStartDate = new DateTime(DateTime.Now.Year-1, 10, 1);
                

                YearlyReport report = new YearlyReport(_dbConnectionService);
                DataSet data = report.GetMonthlyBreakdownData(fiscalYearStartDate);

                ViewBag.FiscalYearStartDate = fiscalYearStartDate;
                return View(data);
        } 
        [Authorize] 
        public IActionResult MonthlyBreakdownC()
        {
                // Define the start of the fiscal year. Adjust this date according to your fiscal year start.
                DateTime fiscalYearStartDate = new DateTime(DateTime.Now.Year-1, 10, 1);
                

                YearlyReport report = new YearlyReport(_dbConnectionService);
                DataSet data = report.GetMonthlyBreakdownData(fiscalYearStartDate);

                ViewBag.FiscalYearStartDate = fiscalYearStartDate;
                return View(data);
        }
        [Authorize] 
        public IActionResult MonthlyBreakdownG()
        {
                               // Define the start of the fiscal year. Adjust this date according to your fiscal year start.
                DateTime fiscalYearStartDate = new DateTime(DateTime.Now.Year-1, 10, 1);
                

                YearlyReport report = new YearlyReport(_dbConnectionService);
                DataSet data = report.GetMonthlyBreakdownData(fiscalYearStartDate);

                ViewBag.FiscalYearStartDate = fiscalYearStartDate;
                return View(data);
        }
        [Authorize]
        public IActionResult MonthlyBreakdownK()
        {
            // Define the start of the fiscal year. Adjust this date according to your fiscal year start.
            DateTime fiscalYearStartDate = new DateTime(DateTime.Now.Year - 1, 10, 1);


            YearlyReport report = new YearlyReport(_dbConnectionService);
            DataSet data = report.GetMonthlyBreakdownData(fiscalYearStartDate);

            ViewBag.FiscalYearStartDate = fiscalYearStartDate;
            return View(data);
        }
        [Authorize] 
        public IActionResult MonthlyBreakdownM()
        {
                                // Define the start of the fiscal year. Adjust this date according to your fiscal year start.
                DateTime fiscalYearStartDate = new DateTime(DateTime.Now.Year-1, 10, 1);
                

                YearlyReport report = new YearlyReport(_dbConnectionService);
                DataSet data = report.GetMonthlyBreakdownData(fiscalYearStartDate);

                ViewBag.FiscalYearStartDate = fiscalYearStartDate;
                return View(data);
        }
        [Authorize]
        public IActionResult Snapshot(string whichMonth)
        {
            DateTime startDate, endDate;
            if (whichMonth == null || whichMonth == "Current")
            {
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                endDate = DateTime.Today.AddDays(-1);
            }
            else
            {
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1);
                endDate = startDate.AddMonths(1).AddDays(-1);
            }
            var curMonthIncomeList = new List<dynamic>();
            var priorMonthIncomeList = new List<dynamic>();
            var curYearIncomeList = new List<dynamic>();
            var priorYearIncomeList = new List<dynamic>();

            var snapshotReport = new SnapshotReport(_dbConnectionService);
            DataSet ds = snapshotReport.SnapshotRetrieve(startDate, endDate);

            if (ds != null && ds.Tables.Count > 0)
            {
                DataTable currentIncomeTable = ds.Tables[0];
                DataTable currentIncomeTable2 = ds.Tables[4];
                decimal Addons0319Sum = 0m;
                foreach (DataRow row2 in currentIncomeTable2.Rows)
                {
                    Addons0319Sum += row2["Addons0319"] != DBNull.Value ? Convert.ToDecimal(row2["Addons0319"]) : 0;
                }
                foreach (DataRow row in currentIncomeTable.Rows)
                {
                    var item = new
                    {
                        TaxableReservationsNetRevenue = row["Taxable Reservations Net Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Taxable Reservations Net Revenue:"]) : 0,
                        NoTaxReservationsNetRevenue = row["No-Tax Reservations Net Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["No-Tax Reservations Net Revenue:"]) : 0,
                        StoreNetRevenue = row["Store Net Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Store Net Revenue:"]) : 0,
                        SnackBarNetRevenue = row["Snack Bar Net Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Snack Bar Net Revenue:"]) : 0,
                        ArcadeGamesRevenue = row["Arcade Games Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Arcade Games Revenue:"]) : 0,
                        KayakShackRevenue = row["Kayak Shack Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Kayak Shack Revenue:"]) : 0,
                        CoffeeTrailerRevenue = row["Coffee Trailer Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Coffee Trailer Revenue:"]) : 0,
                        PropaneSalesRevenue = row["Propane Sales Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Propane Sales Revenue:"]) : 0,
                        LaundryRevenue = row["Laundry Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Laundry Revenue:"]) : 0,
                        ActivitiesEventsRevenue = row["Activities/Events Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Activities/Events Revenue:"]) : 0,
                        GuestServicesRevenue = row["Guest Services Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Guest Services Revenue:"]) : 0,
                        CommissionsRevenue = row["Commissions Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Commissions Revenue:"]) : 0,
                        ParkModelSalesRevenue = row["Park Model Sales Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Park Model Sales Revenue:"]) : 0,
                        MiscellaneousRevenue = row["Miscellaneous Revenue:"] != DBNull.Value ? Convert.ToDecimal(row["Miscellaneous Revenue:"]) : 0,
                        Addons0319 = Addons0319Sum
                    };
                    curMonthIncomeList.Add(item);
                }
            }
            if (ds.Tables.Count > 1)
            {
                DataTable lastMonthIncomeTable = ds.Tables[1];
                DataTable lastMonthIncomeTable2 = ds.Tables[5];
                decimal Addons0319Sum = 0m;
                foreach (DataRow row2 in lastMonthIncomeTable2.Rows)
                {
                    Addons0319Sum += row2["Addons0319"] != DBNull.Value ? Convert.ToDecimal(row2["Addons0319"]) : 0;
                }
                foreach (DataRow row in lastMonthIncomeTable.Rows)
                {
                    var item = new
                    {
                        ResTaxIncome = row["ResTaxIncome"] != DBNull.Value ? Convert.ToDecimal(row["ResTaxIncome"]) : 0,
                        ResNoTaxIncome = row["ResNoTaxIncome"] != DBNull.Value ? Convert.ToDecimal(row["ResNoTaxIncome"]) : 0,
                        StoreIncome = row["StoreIncome"] != DBNull.Value ? Convert.ToDecimal(row["StoreIncome"]) : 0,
                        SnackIncome = row["SnackIncome"] != DBNull.Value ? Convert.ToDecimal(row["SnackIncome"]) : 0,
                        ArcadeIncome = row["ArcadeIncome"] != DBNull.Value ? Convert.ToDecimal(row["ArcadeIncome"]) : 0,
                        KayakIncome = row["KayakIncome"] != DBNull.Value ? Convert.ToDecimal(row["KayakIncome"]) : 0,
                        CoffeeIncome = row["CoffeeIncome"] != DBNull.Value ? Convert.ToDecimal(row["CoffeeIncome"]) : 0,
                        PropaneIncome = row["PropaneIncome"] != DBNull.Value ? Convert.ToDecimal(row["PropaneIncome"]) : 0,
                        LaundryIncome = row["LaundryIncome"] != DBNull.Value ? Convert.ToDecimal(row["LaundryIncome"]) : 0,
                        EventsIncome = row["EventsIncome"] != DBNull.Value ? Convert.ToDecimal(row["EventsIncome"]) : 0,
                        GuestServicesIncome = row["GuestServicesIncome"] != DBNull.Value ? Convert.ToDecimal(row["GuestServicesIncome"]) : 0,
                        CommisionsIncome = row["CommisionsIncome"] != DBNull.Value ? Convert.ToDecimal(row["CommisionsIncome"]) : 0,
                        ParkModelSalesIncome = row["Park Model Sales Revenue"] != DBNull.Value ? Convert.ToDecimal(row["Park Model Sales Revenue"]) : 0,
                        OtherIncome = row["OtherIncome"] != DBNull.Value ? Convert.ToDecimal(row["OtherIncome"]) : 0,
                        Addons0319 = Addons0319Sum
                    };
                    priorMonthIncomeList.Add(item);
                }
            }
            if (ds.Tables.Count > 2)
            {
                DataTable lastYearIncomeTable = ds.Tables[2];
                DataTable lastYearIncomeTable2 = ds.Tables[6];
                decimal Addons0319Sum = 0m;
                foreach (DataRow row2 in lastYearIncomeTable2.Rows)
                {
                    Addons0319Sum += row2["Addons0319"] != DBNull.Value ? Convert.ToDecimal(row2["Addons0319"]) : 0;
                }
                foreach (DataRow row in lastYearIncomeTable.Rows)
                {
                    var item = new
                    {
                        ResTaxIncome = row["ResTaxIncome"] != DBNull.Value ? Convert.ToDecimal(row["ResTaxIncome"]) : 0,
                        ResNoTaxIncome = row["ResNoTaxIncome"] != DBNull.Value ? Convert.ToDecimal(row["ResNoTaxIncome"]) : 0,
                        StoreIncome = row["StoreIncome"] != DBNull.Value ? Convert.ToDecimal(row["StoreIncome"]) : 0,
                        SnackIncome = row["SnackIncome"] != DBNull.Value ? Convert.ToDecimal(row["SnackIncome"]) : 0,
                        ArcadeIncome = row["ArcadeIncome"] != DBNull.Value ? Convert.ToDecimal(row["ArcadeIncome"]) : 0,
                        KayakIncome = row["KayakIncome"] != DBNull.Value ? Convert.ToDecimal(row["KayakIncome"]) : 0,
                        CoffeeIncome = row["CoffeeIncome"] != DBNull.Value ? Convert.ToDecimal(row["CoffeeIncome"]) : 0,
                        PropaneIncome = row["PropaneIncome"] != DBNull.Value ? Convert.ToDecimal(row["PropaneIncome"]) : 0,
                        LaundryIncome = row["LaundryIncome"] != DBNull.Value ? Convert.ToDecimal(row["LaundryIncome"]) : 0,
                        EventsIncome = row["EventsIncome"] != DBNull.Value ? Convert.ToDecimal(row["EventsIncome"]) : 0,
                        GuestServicesIncome = row["GuestServicesIncome"] != DBNull.Value ? Convert.ToDecimal(row["GuestServicesIncome"]) : 0,
                        CommisionsIncome = row["CommisionsIncome"] != DBNull.Value ? Convert.ToDecimal(row["CommisionsIncome"]) : 0,
                        ParkModelSalesIncome = row["Park Model Sales Revenue"] != DBNull.Value ? Convert.ToDecimal(row["Park Model Sales Revenue"]) : 0,
                        OtherIncome = row["OtherIncome"] != DBNull.Value ? Convert.ToDecimal(row["OtherIncome"]) : 0,
                        Addons0319 = Addons0319Sum
                    };
                    curYearIncomeList.Add(item);
                }
            }
            if (ds.Tables.Count > 3)
            {
                DataTable twoYearIncomeTable = ds.Tables[3];
                DataTable currentIncomeTable2 = ds.Tables[7];
                decimal Addons0319Sum = 0m;
                foreach (DataRow row2 in currentIncomeTable2.Rows)
                {
                    Addons0319Sum += row2["Addons0319"] != DBNull.Value ? Convert.ToDecimal(row2["Addons0319"]) : 0;
                }
                foreach (DataRow row in twoYearIncomeTable.Rows)
                {
                    var item = new
                    {
                        ResTaxIncome = row["ResTaxIncome"] != DBNull.Value ? Convert.ToDecimal(row["ResTaxIncome"]) : 0,
                        ResNoTaxIncome = row["ResNoTaxIncome"] != DBNull.Value ? Convert.ToDecimal(row["ResNoTaxIncome"]) : 0,
                        StoreIncome = row["StoreIncome"] != DBNull.Value ? Convert.ToDecimal(row["StoreIncome"]) : 0,
                        SnackIncome = row["SnackIncome"] != DBNull.Value ? Convert.ToDecimal(row["SnackIncome"]) : 0,
                        ArcadeIncome = row["ArcadeIncome"] != DBNull.Value ? Convert.ToDecimal(row["ArcadeIncome"]) : 0,
                        KayakIncome = row["KayakIncome"] != DBNull.Value ? Convert.ToDecimal(row["KayakIncome"]) : 0,
                        CoffeeIncome = row["CoffeeIncome"] != DBNull.Value ? Convert.ToDecimal(row["CoffeeIncome"]) : 0,
                        PropaneIncome = row["PropaneIncome"] != DBNull.Value ? Convert.ToDecimal(row["PropaneIncome"]) : 0,
                        LaundryIncome = row["LaundryIncome"] != DBNull.Value ? Convert.ToDecimal(row["LaundryIncome"]) : 0,
                        EventsIncome = row["EventsIncome"] != DBNull.Value ? Convert.ToDecimal(row["EventsIncome"]) : 0,
                        GuestServicesIncome = row["GuestServicesIncome"] != DBNull.Value ? Convert.ToDecimal(row["GuestServicesIncome"]) : 0,
                        CommisionsIncome = row["CommisionsIncome"] != DBNull.Value ? Convert.ToDecimal(row["CommisionsIncome"]) : 0,
                        ParkModelSalesIncome = row["Park Model Sales Revenue"] != DBNull.Value ? Convert.ToDecimal(row["Park Model Sales Revenue"]) : 0,
                        OtherIncome = row["OtherIncome"] != DBNull.Value ? Convert.ToDecimal(row["OtherIncome"]) : 0,
                        Addons0319 = Addons0319Sum
                    };
                    priorYearIncomeList.Add(item);
                }
            }
            ViewBag.CurMonthIncome = curMonthIncomeList;
            ViewBag.PriorMonthIncome = priorMonthIncomeList;
            ViewBag.CurYearIncome = curYearIncomeList;
            ViewBag.PriorYearIncome = priorYearIncomeList;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.WhichMonth = whichMonth;
            var taxableReservationsListD = new List<dynamic>();
            var lastMonthIncomeListD = new List<dynamic>();
            var lastYearIncomeListD = new List<dynamic>();
            var twoYearIncomeListD = new List<dynamic>();

            var snapshotDepReport = new SnapshotDepReport(_dbConnectionService);
            DataSet dsd = snapshotDepReport.SnapshotDepRetrieve(startDate, endDate);
            System.Diagnostics.Debug.WriteLine($"TaxableReservationsD Count: {taxableReservationsListD.Count}");
            if (dsd != null && dsd.Tables.Count > 0)
            {
                DataTable taxableReservationsTableD = dsd.Tables[0];
                foreach (DataRow row in taxableReservationsTableD.Rows)
                {
                    var item = new
                    {
                        SiteDepositsTaken = row["Site Deposits Taken:"] != DBNull.Value ? Convert.ToDecimal(row["Site Deposits Taken:"]) : 0,
                        SiteDepositsApplied = row["Site Deposits Applied:"] != DBNull.Value ? Convert.ToDecimal(row["Site Deposits Applied:"]) : 0,
                        RentalDepositsTaken = row["Rental Deposits Taken:"] != DBNull.Value ? Convert.ToDecimal(row["Rental Deposits Taken:"]) : 0,
                        RentalDepositsApplied = row["Rental Deposits Applied:"] != DBNull.Value ? Convert.ToDecimal(row["Rental Deposits Applied:"]) : 0,
                        GolfDepositsTaken = row["Golf Deposits Taken:"] != DBNull.Value ? Convert.ToDecimal(row["Golf Deposits Taken:"]) : 0,
                        GolfDepositsApplied = row["Golf Deposits Applied:"] != DBNull.Value ? Convert.ToDecimal(row["Golf Deposits Applied:"]) : 0,
                        VouchersPurchased = row["Vouchers Purchased:"] != DBNull.Value ? Convert.ToDecimal(row["Vouchers Purchased:"]) : 0,
                        VouchersRedeemed = row["Vouchers Redeemed:"] != DBNull.Value ? Convert.ToDecimal(row["Vouchers Redeemed:"]) : 0
                    };
                    taxableReservationsListD.Add(item);
                }

            }
            if (dsd.Tables.Count > 1)
            {
                DataTable lastMonthIncomeTableD = dsd.Tables[1];
                foreach (DataRow row in lastMonthIncomeTableD.Rows)
                {
                    var item = new
                    {
                        SiteDepositsTaken = row[0] != DBNull.Value ? Convert.ToDecimal(row[0]) : 0,
                        SiteDepositsApplied = row[1] != DBNull.Value ? Convert.ToDecimal(row[1]) : 0,
                        RentalDepositsTaken = row[2] != DBNull.Value ? Convert.ToDecimal(row[2]) : 0,
                        RentalDepositsApplied = row[3] != DBNull.Value ? Convert.ToDecimal(row[3]) : 0,
                        GolfDepositsTaken = row[4] != DBNull.Value ? Convert.ToDecimal(row[4]) : 0,
                        GolfDepositsApplied = row[5] != DBNull.Value ? Convert.ToDecimal(row[5]) : 0,
                        VouchersPurchased = row[6] != DBNull.Value ? Convert.ToDecimal(row[6]) : 0,
                        VouchersRedeemed = row[7] != DBNull.Value ? Convert.ToDecimal(row[7]) : 0
                    };
                    lastMonthIncomeListD.Add(item);
                }
                Console.WriteLine($"LastMonthIncomeD Count: {lastMonthIncomeListD.Count}");
            }
            else
            {
                return Json(new { success = false });
            }
            if (dsd.Tables.Count > 2)
            {
                DataTable lastYearIncomeTableD = dsd.Tables[2];
                foreach (DataRow row in lastYearIncomeTableD.Rows)
                {
                    var item = new
                    {
                        SiteDepositsTaken = row.IsNull(0) ? 0 : Convert.ToDecimal(row[0]),
                        SiteDepositsApplied = row.IsNull(1) ? 0 : Convert.ToDecimal(row[1]),
                        RentalDepositsTaken = row.IsNull(2) ? 0 : Convert.ToDecimal(row[2]),
                        RentalDepositsApplied = row.IsNull(3) ? 0 : Convert.ToDecimal(row[3]),
                        GolfDepositsTaken = row.IsNull(4) ? 0 : Convert.ToDecimal(row[4]),
                        GolfDepositsApplied = row.IsNull(5) ? 0 : Convert.ToDecimal(row[5]),
                        VouchersPurchased = row.IsNull(6) ? 0 : Convert.ToDecimal(row[6]),
                        VouchersRedeemed = row.IsNull(7) ? 0 : Convert.ToDecimal(row[7])
                    };
                    lastYearIncomeListD.Add(item);
                }
                Console.WriteLine($"LastYearIncomeListD Count: {lastYearIncomeListD.Count}");
            }
            if (dsd.Tables.Count > 3)
            {
                DataTable twoYearIncomeTableD = dsd.Tables[3];
                foreach (DataRow row in twoYearIncomeTableD.Rows)
                {
                    var item = new
                    {
                        SiteDepositsTaken = row[0] != DBNull.Value ? Convert.ToDecimal(row[0]) : 0,
                        SiteDepositsApplied = row[1] != DBNull.Value ? Convert.ToDecimal(row[1]) : 0,
                        RentalDepositsTaken = row[2] != DBNull.Value ? Convert.ToDecimal(row[2]) : 0,
                        RentalDepositsApplied = row[3] != DBNull.Value ? Convert.ToDecimal(row[3]) : 0,
                        GolfDepositsTaken = row[4] != DBNull.Value ? Convert.ToDecimal(row[4]) : 0,
                        GolfDepositsApplied = row[5] != DBNull.Value ? Convert.ToDecimal(row[5]) : 0,
                        VouchersPurchased = row[6] != DBNull.Value ? Convert.ToDecimal(row[6]) : 0,
                        VouchersRedeemed = row[7] != DBNull.Value ? Convert.ToDecimal(row[7]) : 0
                    };
                    twoYearIncomeListD.Add(item);
                }
                Console.WriteLine($"TwoYearIncomeListD Count: {twoYearIncomeListD.Count}");
            }
            ViewBag.TaxableReservationsD = taxableReservationsListD;
            ViewBag.LastMonthIncomeD = lastMonthIncomeListD;
            ViewBag.LastYearIncomeD = lastYearIncomeListD;
            ViewBag.TwoYearIncomeD = twoYearIncomeListD;

            return View(1);
        
        }
        [Authorize]
        public IActionResult ManageMisc()
        {
            DataSet SpecialAddons = _adminActions.RetrieveAddons();
            return View(SpecialAddons);
        }
        [HttpPost]
        public async Task<string> UpdateAddons(int addIDin, DateTime dateIn, string glIn, string descIn, decimal amountIn)
        {
            string addResult = await _specialAddonsService.UpdateAddons(addIDin, dateIn, glIn, descIn, amountIn);
            if (addResult.Contains("SUCCESS"))
            {
                if (glIn != "0302" && glIn != "0392")
                {
                    addResult = await _specialAddonsService.UpdateMiscFromAddons(dateIn, glIn, descIn, addIDin < 0 ? amountIn * -1 : amountIn);
                }
                else
                {
                    addResult = "FAILURE";
                    GenericRoutines.UpdateAlerts(6, "FATAL ERROR",  "GL Code " + glIn + " was found in Special Addons on " + dateIn.ToShortDateString() + ", IMPORT ABORTED");
                }
            }
            return addResult;
        }
    }
}

