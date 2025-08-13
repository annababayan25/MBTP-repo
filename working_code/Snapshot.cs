using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
namespace MBTP.Retrieval
{
    public class SnapshotReport
    {
        private readonly IConfiguration _configuration;
        public SnapshotReport(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public DataSet SnapshotRetrieve()
        {
            DataSet myDS = new DataSet();
            //actualDate = startDate;
            try
            {
                using (SqlConnection sqlConn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("dbo.RetrieveIncomeSnapshot", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    myDA.Fill(myDS);

                    if (myDS.Tables.Count > 0 && myDS.Tables[0].Rows.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("Data Exists");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No Data");
                    }
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