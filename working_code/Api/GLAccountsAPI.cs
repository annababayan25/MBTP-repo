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
using System.Net;
using System.Text.RegularExpressions;


namespace MBTP.Services {

    public class GLAccountApi 
    {
        private readonly string reconApiUrl = "https://api.newbook.cloud/rest/gl_account_list";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly IDatabaseConnectionService _dbConnectionService;

        public GLAccountApi(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
        
        public async Task<List<GLAccount>> FetchAllGLAccounts()
        {

            var glAccountsList = new List<GLAccount>();

            var requestBody = new
            {
                region = region,
                api_key = apiKey,
        
            };

            int loopCount = 0;
            HttpResponseMessage response = new HttpResponseMessage();
            while (loopCount < 5)
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
                var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                response = await httpClient.PostAsync(reconApiUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"HTTP request failed with status code: {response.StatusCode}");
                    loopCount++;
                    if (loopCount == 5)
                    {
                        return glAccountsList;
                    }
                }
                else
                {
                    break;
                }
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            JObject jsonObject = JObject.Parse(jsonResponse);
            List<string> jsonTokens = new List<string>();

            var result = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
            //Console.WriteLine($"HTTP JSON RESPONSE: {jsonResponse}");
            if (result is null || result.success != "true")
            {
                Console.WriteLine("API response indicates failure.");
                return new List<GLAccount>();
            }

            foreach (var item in result.data)
            {
            
                Console.WriteLine($"Full GL Accounts JSON: {item.ToString()}");

                var gl = new GLAccount
                {
                    GLAccountId = item.gl_account_id,
                    GLAccountCode = item.gl_account_code,
                    GLAccountName = item.gl_account_name
                };


            glAccountsList.Add(gl);
        }
        return glAccountsList;
    }
}

}

