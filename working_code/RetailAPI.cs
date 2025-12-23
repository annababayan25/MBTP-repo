using System.Data;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using MBTP.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic;
using SQLStuff;
using MBTP.Interfaces;
using GenericSupport;

namespace MBTP.Services
{
    public class RetailService
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public RetailService(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
        static SqlConnection sqlConn = new(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("ConnectionStrings")["DefaultConnection"]);
        static SqlConnection sqlConnTest = new(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("ConnectionStrings")["TestConnection"]);
        static ConfigurationSection myConfig = (ConfigurationSection)new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("Heartland");
        static string myKey = new(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("Heartland")["ApiKey"]);
        static string myRptPrefix = new(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("Heartland")["RptStrBase"]);
        public async Task PopulateRetailData(string operation, DateTime dateIn)
        {
            string locationFilter = "";
            if (operation == "Store")
            {
                locationFilter = @"&location.filters=%7B""%24and""%3A%5B%7B""id""%3A%7B""%24in""%3A%5B""100128""%2C""100005""%5D%7D%7D%5D%7D"; 
            }
            string retailPeriod = "&start_date=" + dateIn.ToString("yyyy-MM-dd") + "&end_date=" + dateIn.ToString("yyyy-MM-dd");
            List<RetailGroup> salesEntries = await FetchRetailDataAsync(locationFilter ,retailPeriod);
            List<PaymentsGroup> paymentsEntries = await FetchPaymentDataAsync(locationFilter,retailPeriod);
            decimal taxCollected = await FetchTaxDataAsync(locationFilter,retailPeriod);

            if (salesEntries.Count > 0 || paymentsEntries.Count > 0)
            {
                InsertStoreData(dateIn, salesEntries, paymentsEntries, taxCollected);
            }
            else
            {
                Console.WriteLine("No bookings to display.");
            }
            Console.WriteLine("Run method finished.");
        }

        private static async Task<List<RetailGroup>> FetchRetailDataAsync(string locationFilter, string periodFromTo)
        {
            string myRptSuffix = locationFilter + "&group[]=item.custom%40category&group[]=item.custom%40subcategory&metrics[]=source_sales.net_sales&request_client_uuid=f926e2f2-c08a-4fa7-8248-b00a053a8326&only_grand_total=false";
            var retailEntries = new List<RetailGroup>();

            HttpResponseMessage response = new HttpResponseMessage();
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", myConfig["ApiKey"]);
            Console.WriteLine("Sending Retail HTTP GET request...");
            response = await httpClient.GetAsync(myConfig["RptStrBase"] + "true" + periodFromTo + myRptSuffix);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                return retailEntries;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            jsonResponse = jsonResponse.Replace("source_sales.net_sales", "net_sales").Replace("item.custom@category", "category").Replace("item.custom@subcategory", "subcategory");

            JObject jsonObject = JObject.Parse(jsonResponse);
            decimal net_salesVal = 0.0m;
            string category = "";
            foreach (var jsonOuterToken in jsonObject.Children<JProperty>())
            {
                if (jsonOuterToken.Name == "results")
                {
                    foreach (var jsonMiddleToken in jsonOuterToken.Value.Children<JToken>())
                    {
                        foreach (var jsonInnerToken in jsonMiddleToken.Children<JProperty>())
                        {
                            if (jsonInnerToken.Value.ToString() != "")
                            {
                                if (jsonInnerToken.Name == "subtotal_level" && jsonInnerToken.Value.ToString() == "0")
                                {
                                    break;  // Ignore the grand total child
                                }
                                if (jsonInnerToken.Name == "net_sales")
                                {
                                    net_salesVal = (decimal)jsonInnerToken.Value;
                                }
                                else if (jsonInnerToken.Name == "category")
                                {
                                    category = jsonInnerToken.Value.ToString();
                                }
                                else if (jsonInnerToken.Name == "subcategory")
                                {
                                    var retailEntry = new RetailGroup
                                    {
                                        net_sales = net_salesVal,
                                        category = category,
                                        subcategory = jsonInnerToken.Value.ToString()
                                    };
                                    retailEntries.Add(retailEntry);
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine("Retail Response processing completed...");
            return retailEntries;
        }
        private static async Task<List<PaymentsGroup>> FetchPaymentDataAsync(string locationFilter, string periodFromTo)
        {
            string myRptSuffix = locationFilter + "&group[]=credit_card_payment.type&group[]=date.date&metrics[]=payment.payments_received&metrics[]=payment.payment_type_count&metrics[]=payment.net_payments&sort[]=date.date%2Casc&request_client_uuid=a00294c2-983a-4dbb-971a-f001497a0035";
            var paymentEntries = new List<PaymentsGroup>();

            HttpResponseMessage response = new HttpResponseMessage();
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", myConfig["ApiKey"]);
            Console.WriteLine("Sending HTTP GET Payments request...");
            response = await httpClient.GetAsync(myConfig["RptStrBase"] + "true&include_links=true&charts=%5B%5D&page=1" + periodFromTo + myRptSuffix);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"HTTP Payments request failed with status code: {response.StatusCode}");
                return paymentEntries;
            }
            var jsonResponse = await response.Content.ReadAsStringAsync();
            //jsonResponse = jsonResponse.Replace("source_sales.net_sales", "net_sales").Replace("item.custom@category", "category").Replace("item.custom@subcategory", "subcategory");

            JObject jsonObject = JObject.Parse(jsonResponse);
            decimal netPaymentsTotal = 0.0m;
            foreach (var jsonOuterToken in jsonObject.Children<JProperty>())
            {
                if (jsonOuterToken.Name == "results")
                {
                    foreach (var jsonMiddleToken in jsonOuterToken.Value.Children<JToken>())
                    {
                        foreach (var jsonInnerToken in jsonMiddleToken.Children<JProperty>())
                        {
                            if (jsonInnerToken.Value.ToString() != "" ||(jsonInnerToken.Value.ToString() == "" && jsonInnerToken.Name == "credit_card_payment.type"))
                            {
                                if (jsonInnerToken.Name == "subtotal_level" && jsonInnerToken.Value.ToString() == "0")
                                {
                                    break;  // Ignore the grand total child
                                }
                                if (jsonInnerToken.Name == "payment.net_payments")
                                {
                                    netPaymentsTotal = (decimal)jsonInnerToken.Value;
                                }
                                else if (jsonInnerToken.Name == "credit_card_payment.type")
                                {
                                    var paymentEntry = new PaymentsGroup
                                    {
                                        net_payments = netPaymentsTotal,
                                        payment_type = (jsonInnerToken.Value.ToString() == "" ? "Cash" : jsonInnerToken.Value.ToString())
                                    };
                                    paymentEntries.Add(paymentEntry);
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine("Payments Response processing completed...");
            return paymentEntries;

        }
        private static async Task<decimal> FetchTaxDataAsync(string locationFilter, string periodFromTo)
        {
            string myRptSuffix = locationFilter + "&group[]=date.date&metrics[]=source_sales.net_sales&metrics[]=location_sales_tax.net_amount_collected&sort[]=date.date%2Casc&charts=%5B%5D&request_client_uuid=4acf12d0-3357-42ef-8782-f7d3035c8588";

            HttpResponseMessage response = new HttpResponseMessage();
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", myConfig["ApiKey"]);
            Console.WriteLine("Sending HTTP GET Tax request...");
            response = await httpClient.GetAsync(myConfig["RptStrBase"] + "false&include_links=false&page=1" + periodFromTo + myRptSuffix);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"HTTP Tax request failed with status code: {response.StatusCode}");
                return 0;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();

            JObject jsonObject = JObject.Parse(jsonResponse);
            foreach (var jsonOuterToken in jsonObject.Children<JProperty>())
            {
                if (jsonOuterToken.Name == "results")
                {
                    foreach (var jsonMiddleToken in jsonOuterToken.Value.Children<JToken>())
                    {
                        foreach (var jsonInnerToken in jsonMiddleToken.Children<JProperty>())
                        {
                            if (jsonInnerToken.Value.ToString() != "")
                            {
                                if (jsonInnerToken.Name == "subtotal_level" && jsonInnerToken.Value.ToString() == "0")
                                {
                                    break;  // Ignore the grand total child
                                }
                                if (jsonInnerToken.Name == "location_sales_tax.net_amount_collected")
                                {
                                    Console.WriteLine("Tax Response processing completed...");
                                    return (decimal)jsonInnerToken.Value;    // Found what we need, time to exit
                                }
                            }
                        }
                    }
                }
            }
            return 0;

        }
        private void InsertStoreData(DateTime transDate, List<RetailGroup> SalesEntries, List<PaymentsGroup> PaymentsEntries, decimal TaxCollected)
        {
            decimal Apparel = 0, SeasonalNovelty = 0, OtherNovelty = 0, Alcohol = 0, HardGoods = 0, RVParts = 0, SeasonalMerch = 0, FoodCounter = 0, Food = 0,
            Ice = 0, Stamps = 0, AtSite = 0, PropaneStation = 0, Events = 0, Guest = 0, Cash = 0, CC = 0;
            decimal preparedFoodTax = 1.105m, salesTax = 1.08m;
            string rptDate = transDate.ToString("yyyy-MM-dd");
            foreach (var salesEntry in SalesEntries)
            {
                if (salesEntry.subcategory == "Apparel")    // This subcategory spans multiple categories
                {
                    Apparel += Math.Round(salesEntry.net_sales * salesTax, 2);
                }
                else
                {
                    switch (salesEntry.category)
                    {
                        case "Seasonal - Novelty":
                            SeasonalNovelty += Math.Round(salesEntry.net_sales * salesTax, 2); break;
                        case "Novelty":
                            OtherNovelty += Math.Round(salesEntry.net_sales * salesTax, 2); break;
                        case "Alcohol":
                            Alcohol += Math.Round(salesEntry.net_sales * salesTax, 2); break;
                        case "Grocery - Hard Goods":
                            HardGoods += Math.Round(salesEntry.net_sales * salesTax, 2); break;
                        case "RV Parts":
                            RVParts += Math.Round(salesEntry.net_sales * salesTax, 2); break;
                        case "Seasonal - Store Merch":
                            SeasonalMerch += Math.Round(salesEntry.net_sales * salesTax, 2); break;
                        case "Food Counter":
                            FoodCounter += Math.Round(salesEntry.net_sales * preparedFoodTax, 2); break;
                        case "Grocery - Edible":
                            Food += salesEntry.net_sales; break;
                        case "Grocery - Ice":
                            Ice += salesEntry.net_sales; break;
                        case "Non-Revenue":
                            Stamps += salesEntry.net_sales; break;
                        case "Propane Service":
                            if (salesEntry.subcategory == "Propane Station")
                            {
                                PropaneStation += salesEntry.net_sales;
                            }
                            else
                            {
                                AtSite += salesEntry.net_sales;
                            }
                            break;
                        case "Guest Services":
                            Guest += salesEntry.net_sales; break;
                        default:
                            Events += Math.Round(salesEntry.net_sales * preparedFoodTax, 2);
                            if (transDate >= DateTime.Parse("2025-09-23") && transDate <= DateTime.Parse("2025-10-10")) // spaghetti dinner
                            {
                                if (transDate <= DateTime.Parse("2025-10-07"))
                                {
                                    Events += (salesEntry.net_sales * (salesTax - 1.0m)); // Early tickets double charged tax
                                }
                            }
                            break;
                    }
                }
            }
            foreach (var paymentsEntry in PaymentsEntries)
            {
                if (paymentsEntry.payment_type == "Cash")
                {
                    Cash = paymentsEntry.net_payments;
                }
                else
                {
                    CC += paymentsEntry.net_payments;
                }
            }
            // Create the connection to the database and define the SQl command that calls the stored procedure.  Stop here it there's a problem
            SQLSupport sqlSupport = new SQLSupport(_dbConnectionService);
            if (!sqlSupport.PrepareForImport("UpdateStoreTable"))
            {
                return;
            }
            try
            {
                sqlSupport.AddSQLParameter("@Apparel", SqlDbType.SmallMoney, (double)Apparel);
                sqlSupport.AddSQLParameter("@SeasonalNovelty", SqlDbType.SmallMoney, (double)SeasonalNovelty);
                sqlSupport.AddSQLParameter("@OtherNovelty", SqlDbType.SmallMoney, (double)OtherNovelty);
                sqlSupport.AddSQLParameter("@Alcohol", SqlDbType.SmallMoney, (double)Alcohol);
                sqlSupport.AddSQLParameter("@HardGoods", SqlDbType.SmallMoney, (double)HardGoods);
                sqlSupport.AddSQLParameter("@RVParts", SqlDbType.SmallMoney, (double)RVParts);
                sqlSupport.AddSQLParameter("@SeasonalMerch", SqlDbType.SmallMoney, (double)SeasonalMerch);
                sqlSupport.AddSQLParameter("@FoodCounter", SqlDbType.SmallMoney, (double)FoodCounter);
                sqlSupport.AddSQLParameter("@Food", SqlDbType.SmallMoney, (double)Food);
                sqlSupport.AddSQLParameter("@Ice", SqlDbType.SmallMoney, (double)Ice);
                sqlSupport.AddSQLParameter("@Stamps", SqlDbType.SmallMoney, (double)Stamps);
                sqlSupport.AddSQLParameter("@AtSitePropane", SqlDbType.SmallMoney, (double)AtSite);
                sqlSupport.AddSQLParameter("@PropaneStation", SqlDbType.SmallMoney, (double)PropaneStation);
                sqlSupport.AddSQLParameter("@Events", SqlDbType.SmallMoney, (double)Events);
                sqlSupport.AddSQLParameter("@StoreCC", SqlDbType.SmallMoney, (double)CC);
                sqlSupport.AddSQLParameter("@StoreCash", SqlDbType.SmallMoney, (double)Cash);
                sqlSupport.AddSQLParameter("@TotalTaxCollected", SqlDbType.SmallMoney, (double)TaxCollected);
                _ = sqlSupport.ExecuteStoredProcedure(2);
                SQLSupport sqlSupportG = new SQLSupport(_dbConnectionService);
                if (!sqlSupport.PrepareForImport("UpdateMiscTable"))
                {
                    return;
                }
                sqlSupport.AddSQLParameter("@GuestServices", SqlDbType.SmallMoney, (double)Guest);
                _ = sqlSupport.ExecuteStoredProcedure(2);
            }
            catch (Exception ex)
            {
                GenericRoutines.UpdateAlerts(2, "FATAL ERROR", ex.ToString() + ", IMPORT ABORTED");
                return;
            }
        }
    }
}