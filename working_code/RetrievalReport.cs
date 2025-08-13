using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace MBTP.Retrieval
{
    public class RetrievalReport
    {
        private readonly IConfiguration _configuration;
        public RetrievalReport(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<DataSet> RetrievalOfData(DateTime startDate)
        {
            
            DataSet myDS = new DataSet();
            try
            {
                using (SqlConnection sqlConn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("dbo.RetrieveDailyReportFrontOfficeAdditions", sqlConn)) 
                { 
                    cmd.CommandType = CommandType.StoredProcedure ;
                    cmd.Parameters.Add("@TransDate", SqlDbType.Date);
                    SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                    
                    sqlConn.Open();

                    cmd.Parameters["@TransDate"].Value = startDate;
                    myDS.Clear();
                    myDA.Fill(myDS);

                    if (myDS.Tables.Count > 0 && myDS.Tables[0].Rows.Count > 0)
                    {
                        cmd.CommandText = "dbo.RetrieveDailyReportFrontOfficeMiscAdditions";
                        SqlDataAdapter myDA1b = new SqlDataAdapter(cmd);
                        myDA1b.Fill(myDS,"Miscellaneous");
                        cmd.CommandText = "dbo.RetrieveOpDeductions";
                        SqlDataAdapter myDA2 = new SqlDataAdapter(cmd);
                        myDA2.Fill(myDS,"Deductions");
                        cmd.CommandText = "dbo.RetrieveDailyTransfers";
                        SqlDataAdapter myDA3 = new SqlDataAdapter(cmd);
                        myDA3.Fill(myDS,"Transfers");
                        cmd.CommandText = "dbo.RetrieveDailyManualChecks";
                        SqlDataAdapter myDA4 = new SqlDataAdapter(cmd);
                        myDA4.Fill(myDS,"Checks");
                        // Now create a table to hold weather data
                        DataTable WeatherTable = myDS.Tables.Add("Weather");
                        WeatherTable.Columns.Add("Sunrise", typeof(string));
                        WeatherTable.Columns.Add("Sunset", typeof(string));
                        WeatherTable.Columns.Add("Precip",typeof(decimal));
                        WeatherTable.Columns.Add("Precipcover", typeof(decimal));
                        WeatherTable.Columns.Add("TempMax",typeof(decimal));
                        WeatherTable.Columns.Add("TempMin",typeof(decimal));
                        WeatherTable.Columns.Add("Description", typeof(string));
                        // Fetch weather data for the actual date used in the report
                        using (HttpClient client = new HttpClient())
                        {
                            string location = "Myrtle Beach"; // Replace with your actual city
                            string formattedDate = startDate.ToString("yyyy-MM-dd");
                            string _apiKey = "VETTNNC58YGSD48HA6A6VZ7DG"; // Replace with your actual API key
                            string _baseUri = "https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline";
                            string uri = $"{_baseUri}/{location}/{formattedDate}/{formattedDate}?unitGroup=metric&include=days&key={_apiKey}&contentType=json";

                            //Console.WriteLine($"Requesting weather data for {location} on {formattedDate}");
                            HttpResponseMessage response = await client.GetAsync(uri);
                            string json = await response.Content.ReadAsStringAsync();

                            //Console.WriteLine($"API Response: {json}"); // Log the response

                            try
                            {
                                JObject weatherData = JObject.Parse(json);
                                var days = weatherData["days"];

                                if (days != null)
                                {
                                    foreach (var day in days)
                                    {
                                        string? sunrise = day["sunrise"]?.ToObject<string>();
                                        string? sunset = day["sunset"]?.ToObject<string>();
                                        double? precip = day["precip"]?.ToObject<double>();
                                        double? precipcover = day["precipcover"]?.ToObject<double>();
                                        double? tempmax = day["tempmax"]?.ToObject<double>();
                                        double? tempmin = day["tempmin"]?.ToObject<double>();
                                        string? description = day["description"]?.ToObject<string>();
                                        //Console.WriteLine($"Date: {day["datetime"]}");
                                        //Console.WriteLine($"Sunrise: {sunrise}");
                                        //Console.WriteLine($"Sunset: {sunset}");
                                        //Console.WriteLine($"Precipitation: {precip}");
                                        //Console.WriteLine($"Cover: {precipcover}");
                                        //Console.WriteLine($"Temp: {temperature}");
                                        //Console.WriteLine($"Description: {description}");
                                        WeatherTable.Rows.Add(sunrise, sunset, precip, precipcover, tempmax, tempmin,description);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error parsing JSON: {ex.Message}");
                                Console.WriteLine($"Response content: {json}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No Data for " + startDate.ToString("yyyy-MM-dd"));
                        Console.WriteLine("No Data for " + startDate.ToString("yyyy-MM-dd"));
                    }

                    sqlConn.Close();
                }
                return myDS;
            }
            catch (SqlException sqlEx)
            {
                System.Diagnostics.Debug.WriteLine("SQL error: " + sqlEx.Message);
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("General error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack Trace: " + ex.StackTrace);
                throw;
            }
        }
    }
}