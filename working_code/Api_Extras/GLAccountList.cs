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
    public class GLAccounts : NewbookBaseApi
    {
        private readonly IDatabaseConnectionService _dbConnectionService;
        public GLAccounts(HttpClient client, IDatabaseConnectionService dbConnectionService) : base(client)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task<List<GLAccount>> FetchGLAccountsAsync()
        {
            var dataOffset = 0;
            var dataCount = 100;
            var dataTotal = 100000;
            var glAccountList = new List<GLAccount>();

            while (dataOffset < dataTotal)
            {
                var requestBody = new
                {
                    region = region,
                    api_key = apiKey,
                    show_inactive = "false"
                };

                var json = await PostAsync("gl_account_list", requestBody);

                var result = JsonConvert.DeserializeObject<dynamic>(json.ToString());

                // Console.WriteLine($"Sending request at offset {dataOffset} of {dataTotal} (batch size {dataCount})");
                 
                if (result == null || result.success != "true") break;
                dataTotal = result.data_total;
                dataOffset += dataCount;

                foreach (var item in result.data)
                {
                    var gls = new GLAccount
                    {
                        GlAccountId = item.gl_account_id,
                        GlAccountCode = item.gl_account_code,
                        GlAccountName = item.gl_account_name,
                        LongDescription = item.long_description,
                        Refundable = item.refundable,
                        GlGroupId = item.gl_group_id,
                        GlGroupName = item.gl_group_name,
                        Active = item.active
                    };

                    glAccountList.Add(gls);   
                    var jsonFile = "glAccounts.json";
                    var jsonOutput = JsonConvert.SerializeObject(glAccountList, Formatting.Indented);
                    File.WriteAllText(jsonFile, jsonOutput);
                }
                 
            } 
            return glAccountList;
        }
        
    }
}
