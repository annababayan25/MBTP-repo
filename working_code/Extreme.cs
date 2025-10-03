using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MBTP.Models;
using Newtonsoft.Json;
using MBTP.Interfaces;

namespace MBTP.Extreme
{
    public class ExtremeService
    {
        private readonly HttpClient _httpClient;
        private readonly IDatabaseConnectionService _dbConnectionService;

        public ExtremeService(HttpClient httpClient, IDatabaseConnectionService dbConnectionService)
        {
            _httpClient = httpClient;
            _dbConnectionService = dbConnectionService;
        }

        public async Task<List<Device>> FetchExtremeKey()
        {
            var requestBody = new
            {
                username = "mbtpadmin@mbtravelpark.com",
                password = "Dashboard2025!"
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("login", content);
            if (!response.IsSuccessStatusCode)
            {
                return new List<Device>();
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            ExtremeKey? extremeKey = JsonConvert.DeserializeObject<ExtremeKey>(jsonResponse);

            if (extremeKey is null)
            {
                return new List<Device>();
            }

            // attach bearer token
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", extremeKey.access_token);

            // fetch first page of devices
            response = await _httpClient.GetAsync("devices?page=1&limit=100&fields=HOSTNAME&fields=CONNECTED&fields=LAST_CONNECT_TIME&fields=LOCATION_ID&deviceTypes=REAL&async=false");
            if (!response.IsSuccessStatusCode)
            {
                return new List<Device>();
            }

            jsonResponse = await response.Content.ReadAsStringAsync();
            DeviceList? deviceList = JsonConvert.DeserializeObject<DeviceList>(jsonResponse);
            if (deviceList == null)
            {
                return new List<Device>();
            }

            // fetch remaining pages if any
            for (int page = 2; page <= deviceList.total_pages; page++)
            {
                var pagedUrl = $"devices?page={page}&limit=100&fields=HOSTNAME&fields=CONNECTED&fields=LAST_CONNECT_TIME&fields=LOCATION_ID&deviceTypes=REAL&async=false";
                response = await _httpClient.GetAsync(pagedUrl);
                if (!response.IsSuccessStatusCode) continue;

                jsonResponse = await response.Content.ReadAsStringAsync();
                DeviceList? pagedList = JsonConvert.DeserializeObject<DeviceList>(jsonResponse);
                if (pagedList != null)
                {
                    deviceList.Data.AddRange(pagedList.Data);
                }
            }

            // fetch floors
            response = await _httpClient.GetAsync("locations/floor?page=1&limit=100");
            if (!response.IsSuccessStatusCode)
            {
                return new List<Device>();
            }

            jsonResponse = await response.Content.ReadAsStringAsync();
            FloorList? floorList = JsonConvert.DeserializeObject<FloorList>(jsonResponse);
            if (floorList == null)
            {
                return new List<Device>();
            }

            // enrich devices with floor hub name
            for (int i = deviceList.Data.Count - 1; i >= 0; i--)
            {
                Device device = deviceList.Data[i];
                if (device.location_id != null)
                {
                    var floor = floorList.Data.Find(f => f.id == device.location_id);
                    device.hubName = floor != null ? floor.name : "Unknown";
                }
                else
                {
                    device.hubName = "No Location";
                }

                if (device.hubName == "Spares")
                {
                    deviceList.Data.RemoveAt(i);
                }
            }

            return new List<Device>(deviceList.Data);
        }
    }
}
