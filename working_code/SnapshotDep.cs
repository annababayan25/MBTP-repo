using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
namespace MBTP.Retrieval
{
    public class SnapshotDepReport
    {
        private readonly IConfiguration _configuration;
        public SnapshotDepReport(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public DataSet SnapshotDepRetrieve()
        {
            DataSet myDSD = new DataSet();
            //actualDate = startDate;
            try
            {
                using (SqlConnection sqlConn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("dbo.FetchStoreTransactionsCount", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    cmd.CommandText = "dbo.RetrieveDepositsSnapshot";
                    myDA.Fill(myDSD);

                    if (myDSD.Tables.Count > 0 && myDSD.Tables[0].Rows.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("Data Exists");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No Data");
                    }
                    sqlConn.Close();
                }
                return myDSD;
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