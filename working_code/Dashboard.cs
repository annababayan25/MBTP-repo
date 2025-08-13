using System;
using System.Data;
using System.Threading.Tasks;
using MBTP.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using MBTP.Interfaces;

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
                using (SqlCommand cmd = new SqlCommand("dbo.RetrieveDashboardData", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                    SqlDataAdapter myDA2 = new SqlDataAdapter(cmd);
                    SqlDataAdapter myDA3 = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    myDS.Clear();
                    myDA.Fill(myDS);
                    cmd.CommandText = "dbo.RetrieveDashboardStoreData";
                    myDA2.Fill(myDS,"Store");
                    cmd.CommandText = "dbo.RetrieveDataErrors";
                    myDA3.Fill(myDS,"Alerts");
                    sqlConn.Close();
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