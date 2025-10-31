using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MBTP.Models;
using Newtonsoft.Json;
using MBTP.Interfaces;
using System.IO;

namespace MBTP.Services
{
    public class InventoryApi
    {
        private readonly string apiUrl = "https://api.newbook.cloud/rest/inventory_item_list";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string regionString = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly IDatabaseConnectionService _dbConnectionService;

        public InventoryApi(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        // Public entry points
        public async Task<List<InventoryItems>> PopulateInventory()
        {
            Console.WriteLine("Run method started.");

            var inventory = await FetchAllInventoryAsync();


            Console.WriteLine($"Run method finished. Number of IIs: {inventory.Count}");
            return inventory;
        }

        private async Task<List<InventoryItems>> FetchAllInventoryAsync()
        {

            var requestBody = new
            {
                region = regionString,
                api_key = apiKey,
                data_limit = 500,
            };

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
            var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            Console.WriteLine("Sending HTTP POST request for inventory_item_list...");

            var response = await httpClient.PostAsync(apiUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                return new List<InventoryItems>();
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

            if (result is null || result.success != "true")
            {
                Console.WriteLine("API response indicates failure.");
                return new List<InventoryItems>();
            }

            var items = new List<InventoryItems>();
            var lines = new List<string>();
            var sortedItems = new List<InventoryItems>();
            if (result != null)
            {
                if (result.data != null)
                {
                    foreach (var item in result.data)
                    {
                        var parsedItem = JsonConvert.DeserializeObject<InventoryItems>(item.ToString());
                        if (parsedItem != null)
                            items.Add(parsedItem);
                    }
                    sortedItems = items
                        .OrderBy(i => int.TryParse(i.GlAccountId, out var id) ? id : int.MaxValue)
                        .ToList();

                    foreach (var item in sortedItems)
                    {
                        lines.Add($"{item.GlAccountId} | {item.GlCategoryId} | {item.Name} | {item.Description} | {item.Amount}");
                    }

                    string filePath = "inventory.txt";
                    File.WriteAllLines(filePath, lines);
                }
            }
            return sortedItems;
        }
    }
}