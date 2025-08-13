using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class WeatherService
{
    private readonly string _apiKey = "VETTNNC58YGSD48HA6A6VZ7DG"; // Replace with your actual API key
    private readonly string _baseUri = "https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline";

    public async Task<(bool wasRaining, double temperature)> WasItRainingOnDate(string location, DateTime date)
    {
        using (HttpClient client = new HttpClient())
        {
            string formattedDate = date.ToString("yyyy-MM-dd");
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
                        double? precipitation = day["precipcover"]?.ToObject<double>();
                        double temperature = day["temp"]?.ToObject<double>() ?? 0.0;

                        //Console.WriteLine($"Date: {day["datetime"]}");
                        //Console.WriteLine($"Precipitation: {precipitation}");
                        //Console.WriteLine($"Temp: {temperature}");

                        if (precipitation > 50)
                        {
                            return (true, temperature);
                        }
                        return(false, temperature);
                    }
                }

                return (false,0.0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing JSON: {ex.Message}");
                Console.WriteLine($"Response content: {json}");
                return (false,0.0); // Default to false if parsing fails or data is not available
            }
        }
    }
}