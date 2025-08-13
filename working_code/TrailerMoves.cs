using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using MBTP.Interfaces;


namespace MBTP.Retrieval
{
    public class TrailerMovesReport
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public TrailerMovesReport(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public DataSet RetrieveTrailerMovesData(DateTime moveDate)
        {
            DataSet myDS = new DataSet();

            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand("dbo.RetrieveTrailerMovesData", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@MoveDate", moveDate);
                    SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    myDS.Clear();
                    myDA.Fill(myDS);
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