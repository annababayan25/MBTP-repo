using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Interfaces;

namespace MBTP.Services
{
    public class AdministrationService
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public AdministrationService(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public DataSet ReviewDistinctAlerts()
        {
            DataSet myDS = new DataSet();

            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand("dbo.RetrieveDistinctAlerts", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
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
        
        public async Task<bool> PostBlackoutDate(string blackoutKey, string reason = null)
        {
            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand("dbo.UpdateBlackoutDates", sqlConn))
                {
                    DateTime dummy = DateTime.Now;
                    string pcID = blackoutKey.Substring(10);
                    string myDate = blackoutKey.Substring(0, 10);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@StartDate", myDate);
                    cmd.Parameters.AddWithValue("@EndDate", myDate);
                    cmd.Parameters.AddWithValue("@pcID", pcID);
                    cmd.Parameters.AddWithValue("@DeleteNeeded", 255);
                    cmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@WorkingYr", 0);
                    cmd.Parameters.AddWithValue("@WorkingMonth", 0);
                    cmd.Parameters.Add("@status", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;
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
        
        public DataSet RetrieveAddons()
        {
            DataSet myDS = new DataSet();

            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand("dbo.RetrieveSpecialAddons", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    myDS.Clear();
                    myDA.Fill(myDS);
                    SqlDataAdapter myDA2 = new SqlDataAdapter(cmd);
                    cmd.CommandText = "dbo.RetrieveSpecialAddonGLcodes";
                    myDA2.Fill(myDS, "GLcodes");
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