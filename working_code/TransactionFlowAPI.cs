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

namespace MBTP.Services
{
    public class TransactionFlowAPI
    {
        private readonly string apiUrl = "https://api.newbook.cloud/rest/reports_transaction_flow";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly IDatabaseConnectionService _dbConnectionService;
        public TransactionFlowAPI(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

    }
    
    public async Task PopulateTransactionFlow(DateTime startDate, DateTime endDate)
        {

        }

        private async Task InsertTransactionFlowTable(TransactionFlow transaction, SqlConnection sqlConn)
        {
            using (SqlCommand cmd = new SqlCommand(@"dbo.UpdateTransactionFlowTable", sqlConn)
            {
            Add.
        }
    }
    }
}