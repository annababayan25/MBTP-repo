using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MBTP.Interfaces;

namespace MBTP.Retrieval
{
    public class SnapshotDepReport
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public SnapshotDepReport(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public DataSet SnapshotDepRetrieve(DateTime startDate, DateTime endDate)
        {
            DataSet myDSD = new DataSet();
            //actualDate = startDate;
            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand("dbo.RetrieveDepositsSnapshot", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);
                    SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                    sqlConn.Open();
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