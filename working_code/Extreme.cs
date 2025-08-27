using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MBTP.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MBTP.Interfaces;

namespace MBTP.Extreme
{
    public class ExtremeService
    {
        private string apiUrl = "https://api.extremecloudiq.com/login";
        private readonly string username = "mbtpadmin@mbtravelpark.com";
        private readonly string password = "Dashboard2025!";
        private readonly IDatabaseConnectionService _dbConnectionService;
        public ExtremeService(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
        public async Task<List<Device>> FetchExtremeKey()
        {
            var requestBody = new
            {
                username = username,
                password = password
            };

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(apiUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                return new List<Device>();
            }
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
            if (result is null)
            {
                return new List<Device>();
            }
            ExtremeKey? extremeKey = JsonConvert.DeserializeObject<ExtremeKey>(jsonResponse);
            //ExtremeKey? extremeKey = null;
            if (extremeKey is null)
            {
                return new List<Device>();
            }
            apiUrl = "https://api.extremecloudiq.com/devices?page=1&limit=100&fields=HOSTNAME&fields=CONNECTED&fields=LAST_CONNECT_TIME&fields=LOCATION_ID&deviceTypes=REAL&async=false";
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", extremeKey.access_token);

            response = await httpClient.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                return new List<Device>();
            }
            jsonResponse = await response.Content.ReadAsStringAsync();
            result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
            if (result is null)
            {
                return new List<Device>();
            }
            DeviceList deviceList = JsonConvert.DeserializeObject<DeviceList>(jsonResponse);
            // Check to see if there are more pages to process
            if(deviceList.total_pages > 1)
            {
                for(int page = 2; page <= deviceList.total_pages; page++)
                {
                    string pagedUrl = $"https://api.extremecloudiq.com/devices?page={page}&limit=100&fields=HOSTNAME&fields=CONNECTED&fields=LAST_CONNECT_TIME&fields=LOCATION_ID&deviceTypes=REAL&async=false";
                    response = await httpClient.GetAsync(pagedUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }
                    jsonResponse = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                    if (result is null)
                    {
                        continue;
                    }
                    DeviceList pagedList = JsonConvert.DeserializeObject<DeviceList>(jsonResponse);
                    if(pagedList is not null)
                    {
                        deviceList.Data.AddRange(pagedList.Data);
                    }
                }
            }
            // now get the floor list
            apiUrl = "https://api.extremecloudiq.com/locations/floor?page=1&limit=100";
            response = await httpClient.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                return new List<Device>();
            }
            jsonResponse = await response.Content.ReadAsStringAsync();
            result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
            if (result is null)
            {
                return new List<Device>();
            }
            FloorList floorList = JsonConvert.DeserializeObject<FloorList>(jsonResponse);
            // now we have both lists, we can add the hub location to the device list
            for (int i = deviceList.Data.Count - 1; i >= 0; i--)
            {
                Device device = deviceList.Data[i];
                if (device.location_id != null)
                {
                    var floor = floorList.Data.Find(f => f.id == device.location_id);
                    if (floor != null)
                    {
                        device.hubName = floor.name;
                    }
                    else
                    {
                        device.hubName = "Unknown";
                    }
                }
                else
                {
                    device.hubName = "No Location";
                }
                if(device.hubName == "Spares")
                {
                    deviceList.Data.RemoveAt(i);
                }
            }
            return new List<Device>(deviceList.Data);
        }
    }
}


