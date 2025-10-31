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
using System.Globalization;

namespace MBTP.Services
{
    public class ChargesApi : NewbookBaseApi
    {
        private readonly IDatabaseConnectionService _dbConnectionService;
        public ChargesApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task<List<Payments_Charges>> PopulateCharges(DateTime startDate, DateTime endDate)
        {
            var dataOffset = 0;
            var dataCount = 100;
            var dataTotal = 100000;
            var chargesList = new List<Payments_Charges>();

            while (dataOffset < dataTotal)
            {
                var requestBody = new
                {
                    region = region,
                    api_key = apiKey,
                    data_count = dataCount,
                    data_offset = dataOffset,
                    period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    account_for = "bookings"
                };

                var json = await PostAsync("charges_list", requestBody);

                var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());

                // Console.WriteLine($"Sending request at offset {dataOffset} of {dataTotal} (batch size {dataCount})");
                 
                if (result == null || result.success != "true") break;
                dataTotal = result.data_total;
                dataOffset += dataCount;

                foreach (var item in result.data)
                {
                    var charges = new Payments_Charges
                    {
                        Id = item.id,
                        AccountId = item.account_id,
                        AccountFor = item.account_for,
                        AccountForId = item.account_for_id,
                        AccountForName = item.account_for_name,
                        Description = item.description,
                        Amount = item.amount,
                        GeneratedWhen = item.generated_when,
                        VoidedWhen = item.voided_when,
                    };

                    // This is to filter through the charges and add only charges that havent been voided for deposits held
                    if ((charges.VoidedWhen != null && (charges.VoidedWhen <= charges.GeneratedWhen)) || (charges.AccountForName.Contains("BLOCKED", StringComparison.OrdinalIgnoreCase)))
                    {
                       continue; 
                    }
                    else
                    {
                        chargesList.Add(charges);
                    }
                }
                          
                 var jsonFile = "charges.json";
                 var jsonOutput = JsonConvert.SerializeObject(chargesList, Formatting.Indented);
                 File.WriteAllText(jsonFile, jsonOutput);
            } 
            // Console.WriteLine("Total Charges: " + chargesList.Count());
            return chargesList;
        }
        
    }
}
