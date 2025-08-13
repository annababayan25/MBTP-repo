using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MBTP.Interfaces;

namespace MBTP.Retrieval
{
    public class SnapshotReport
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public SnapshotReport(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public DataSet SnapshotRetrieve(DateTime startDate, DateTime endDate)
        {
            DataSet myDS = new DataSet();
            //actualDate = startDate;
            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand("dbo.RetrieveIncomeSnapshot", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
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