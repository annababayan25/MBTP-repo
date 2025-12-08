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
    public class PaymentsApi : NewbookBaseApi
    {
        private readonly IDatabaseConnectionService _dbConnectionService;
        public PaymentsApi(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task PopulatePayments(DateTime startDate, DateTime endDate)
        {
            var body = new
            {
                region = region,
                api_key = apiKey,
                period_from = startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                period_to = endDate.ToString("yyyy-MM-dd HH:mm:ss"),
                get_applied_items = "true"
            };

            var json = await PostAsync("payments_list", body);

            var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());
            
            var payments = new List<Payments_Payments>();

            foreach (var item in result.data)
            {
                var payment = new Payments_Payments
                {
                    Id = item.id,
                    AccountId = item.account_id,
                    AccountFor = item.account_for,
                    AccountForId = item.account_for_id,
                    AccountForName = item.account_for_name,
                    Type = item.type,
                    TransactionMethod = item.transaction_method,
                    Description = item.description,
                    Amount = item.amount != null ? (decimal?)item.amount : null,
                    GeneratedWhen = item.generated_when != null ? (DateTime)item.generated_when : DateTime.MinValue,
                    VoidedWhen = item.voided_when,
                    Charges = JsonConvert.DeserializeObject<List<Charge>>(item.charges?.ToString() ?? "[]"),
                    Credits = JsonConvert.DeserializeObject<List<Credits>>(item.credits?.ToString() ?? "[]"),
                    Taxes = JsonConvert.DeserializeObject<List<Tax>>(item.taxes?.ToString() ?? "[]")
                };


                payments.Add(payment);
                var outputFile = "payments.txt";
                File.AppendAllText(outputFile, item.ToString() + Environment.NewLine);
            }
            

        }
        
    }
}
