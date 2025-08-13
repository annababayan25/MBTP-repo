using System;
using System.Data;
using System.Threading.Tasks;
using MBTP.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace MBTP.Retrieval
{
    public class ExpressCheckinsReport
    {
        private readonly IConfiguration _configuration;

        public ExpressCheckinsReport(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DataSet RetrieveExpressCheckinsData(DateTime checkinDate)
        {
            DataSet myDS = new DataSet();

            try
            {
                using (SqlConnection sqlConn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("dbo.RetrieveUnclaimedCheckins", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@CheckinDate", checkinDate);
                    SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                    SqlDataAdapter myDA2 = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    myDS.Clear();
                    myDA.Fill(myDS);
                    cmd.Parameters.Clear();
                    cmd.CommandText = "dbo.RetrieveTodaysClaims";
                    myDA2.Fill(myDS, "Todays");
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
        public async Task<bool> PostClaimedExpress(ExpressCheckinsClaimed claimToProcess)
        {
            try
            {
                using (SqlConnection sqlConn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("dbo.UpdateClaimedCheckins", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@BookingID", claimToProcess.BookingId);
                    cmd.Parameters.AddWithValue("@ClaimedDate", claimToProcess.ClaimedDate);
                    cmd.Parameters.Add("@status", SqlDbType.NVarChar, 4000);
                    cmd.Parameters["@status"].Direction = ParameterDirection.Output;
                    sqlConn.Open();
                    await cmd.ExecuteNonQueryAsync();
                    sqlConn.Close();
                    if ((string)cmd.Parameters["@status"].Value == "SUCCESS")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                System.Diagnostics.Debug.WriteLine("SQL error: " + sqlEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("General error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack Trace: " + ex.StackTrace);
                return false;
            }
        }
    }
}