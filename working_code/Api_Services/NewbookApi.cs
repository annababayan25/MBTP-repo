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
using Newtonsoft.Json.Linq;

public abstract class NewbookBaseApi
{
    protected readonly HttpClient _client;
    protected readonly string _username = "myrtle_beach";
    protected readonly string _password = "Gemb$np(QqEnB9V3";
    protected readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
    protected readonly string region = "us";
    
    protected NewbookBaseApi(HttpClient client)
    {
        _client = client;
        _client.BaseAddress = new Uri("https://api.newbook.cloud/rest/");
        _client.Timeout = TimeSpan.FromMinutes(20);

        var authToken = Encoding.ASCII.GetBytes($"{_username}:{_password}");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));
    }

    protected async Task<JObject> PostAsync(string endpoint, object requestBody)
    {
        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(endpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Newbook API failed: {response.StatusCode}");
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JObject.Parse(jsonResponse);
    }
}
