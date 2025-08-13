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

namespace MBTP.Services
{
    public class RetailService
    {
        static SqlConnection sqlConn = new(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("ConnectionStrings")["DefaultConnection"]);
        static ConfigurationSection myConfig = (ConfigurationSection)new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("Heartland");
        static string myKey = new(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("Heartland")["ApiKey"]);
        static string myRptPrefix = new(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("Heartland")["RptStrBase"]);
        //static 
        public static async Task PopulateRetailData(DateTime dateIn)
        {
            string retailPeriod = "&start_date=" + dateIn.ToString("yyyy-MM-dd") + "&end_date=" + dateIn.ToString("yyyy-MM-dd");
            List<RetailGroup> salesEntries = await FetchRetailDataAsync(retailPeriod);
            List<PaymentsGroup> paymentsEntries = await FetchPaymentDataAsync(retailPeriod);
            decimal taxCollected = await FetchTaxDataAsync(retailPeriod);

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

        private static async Task<List<RetailGroup>> FetchRetailDataAsync(string periodFromTo)
        {
            string myRptSuffix = "&group[]=item.custom%40category&group[]=item.custom%40subcategory&metrics[]=source_sales.net_sales&request_client_uuid=f926e2f2-c08a-4fa7-8248-b00a053a8326&only_grand_total=false";
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
        private static async Task<List<PaymentsGroup>> FetchPaymentDataAsync(string periodFromTo)
        {
            string myRptSuffix = "&group[]=credit_card_payment.type&group[]=date.date&metrics[]=payment.payments_received&metrics[]=payment.payment_type_count&metrics[]=payment.net_payments&sort[]=date.date%2Casc&request_client_uuid=a00294c2-983a-4dbb-971a-f001497a0035";
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
        private static async Task<decimal> FetchTaxDataAsync(string periodFromTo)
        {
            string myRptSuffix = "&group[]=date.date&metrics[]=source_sales.net_sales&metrics[]=location_sales_tax.net_amount_collected&sort[]=date.date%2Casc&charts=%5B%5D&request_client_uuid=4acf12d0-3357-42ef-8782-f7d3035c8588";

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
        private static void InsertStoreData(DateTime transDate, List<RetailGroup> SalesEntries, List<PaymentsGroup> PaymentsEntries, decimal TaxCollected)
        {
            decimal Apparel = 0, SeasonalNovelty = 0, OtherNovelty = 0, Alcohol = 0, HardGoods = 0, RVParts = 0, SeasonalMerch = 0, FoodCounter = 0, Food = 0,
            Ice = 0, Stamps = 0, AtSite = 0, PropaneStation = 0, Events = 0, Cash = 0, CC = 0;
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
                        default:
                            Events += salesEntry.net_sales; break;
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
            sqlConn.Open();
            using (SqlCommand command = new SqlCommand("dbo.UpdateStoreTable", sqlConn))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@TransDate", rptDate);
                command.Parameters.AddWithValue("@Apparel", Apparel);
                command.Parameters.AddWithValue("@SeasonalNovelty", SeasonalNovelty);
                command.Parameters.AddWithValue("@OtherNovelty", OtherNovelty);
                command.Parameters.AddWithValue("@Alcohol", Alcohol);
                command.Parameters.AddWithValue("@HardGoods", HardGoods);
                command.Parameters.AddWithValue("@RVParts", RVParts);
                command.Parameters.AddWithValue("@SeasonalMerch", SeasonalMerch);
                command.Parameters.AddWithValue("@FoodCounter", FoodCounter);
                command.Parameters.AddWithValue("@Food", Food);
                command.Parameters.AddWithValue("@Ice", Ice);
                command.Parameters.AddWithValue("@Stamps", Stamps);
                command.Parameters.AddWithValue("@AtSitePropane", AtSite);
                command.Parameters.AddWithValue("@PropaneStation", PropaneStation);
                command.Parameters.AddWithValue("@Events", Events);
                command.Parameters.AddWithValue("@StoreCC", CC);
                command.Parameters.AddWithValue("@StoreCash", Cash);
                command.Parameters.AddWithValue("@TotalTaxCollected", TaxCollected);
                command.Parameters.Add("@status", SqlDbType.NVarChar, 4000);
                command.Parameters["@status"].Direction = ParameterDirection.Output;
                command.ExecuteNonQuery();
                Console.WriteLine(command.Parameters["@status"].Value.ToString());
            }
            sqlConn.Close();
        }
    }
}