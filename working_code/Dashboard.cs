using System;
using System.Data;
using System.Threading.Tasks;
using MBTP.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MBTP.Models;
using Newtonsoft.Json;
using MBTP.Extreme;

namespace MBTP.Retrieval
{
    public class Dashboard
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public Dashboard(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public DataSet RetrieveDashboardData()
        {
            DataSet myDS = new DataSet();

            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                {
                    sqlConn.Open();
                    myDS.Clear();

                    using (SqlCommand cmd = new SqlCommand("dbo.RetrieveDashboardData", sqlConn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(myDS);
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand("dbo.RetrieveDashboardStoreData", sqlConn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(myDS, "Store");
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand("dbo.RetrieveDataErrors", sqlConn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(myDS, "Alerts");
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand("dbo.RetrieveBlackoutState", sqlConn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@StartDate", DateTime.Today);
                        cmd.Parameters.AddWithValue("@EndDate", DateTime.Today);
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(myDS, "Blackout");
                        }
                    }
                    sqlConn.Close();
                    List<Device> devices = new List<Device>(Task.Run(() => new ExtremeService(_dbConnectionService).FetchExtremeKey()).Result);
                    // Now we'll add a new DataTable for AP Status
                    DataTable apStatusTable = new DataTable("APStatus");
                    apStatusTable.Columns.Add("Connected", typeof(bool));
                    apStatusTable.Columns.Add("APLocation", typeof(string)); 
                    apStatusTable.Columns.Add("LastConnectTime", typeof(DateTime));
                    apStatusTable.Columns.Add("HubName", typeof(string));
                    foreach (var device in devices)
                    {
                        DataRow row = apStatusTable.NewRow();
                        row["Connected"] = device.Connected;
                        row["APLocation"] = device.hostname ?? "Unknown";
                        row["LastConnectTime"] = device.last_connect_time;
                        row["HubName"] = device.hubName ?? "Unknown";
                        apStatusTable.Rows.Add(row);
                    }
                    myDS.Tables.Add(apStatusTable);
                }
                return myDS;
            }
            catch (SqlException sqlEx)
            {
                System.Diagnostics.Debug.WriteLine("SQL error: " + sqlEx.Message);
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("General error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack Trace: " + ex.StackTrace);
                throw;
            }
        }
    }
}

