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

    public class ReconApi 
    {
        private readonly string reconApiUrl = "https://api.newbook.cloud/rest/reports_reconciliation";
        private readonly string transactionFlowApiUrl = "https://api.newbook.cloud/rest/reports_transaction_flow";
        private readonly string apiKey = "instances_1b18c45bae491e9564647b2cb2ef376a";
        private readonly string region = "us";
        private readonly string username = "myrtle_beach";
        private readonly string password = "Gemb$np(QqEnB9V3";
        private readonly TransactionFlowAPI _transactionApi;

        private readonly IDatabaseConnectionService _dbConnectionService;
        public ReconApi(IDatabaseConnectionService dbConnectionService, TransactionFlowAPI transactionApi)
        {
            _dbConnectionService = dbConnectionService;
            _transactionApi = transactionApi;
        }
        
        public async Task PopulateRecons(DateTime startDate, DateTime endDate) 
        {
            Console.WriteLine("Run method started for reconciliation report");

            var recons = await FetchAllRecons(startDate, endDate);
            if (recons.Count > 0)
            {
                using SqlConnection sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                // Insert bookings
                foreach (var recon in recons)
                {
                    await In