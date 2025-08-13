using System;
using System.Data;
using System.Configuration;
using System.Threading.Tasks;
using MBTP.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using MBTP.Logins;
using MBTP.Interfaces;

namespace MBTP.Retrieval
{
    public class AccessLevelsActions
    {
         private readonly IDatabaseConnectionService _dbConnectionService;
        public AccessLevelsActions(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public DataSet RetrieveAccessLevels()
        {
            DataSet myDS = new DataSet();
            try
            {

                using (var sqlConn = _dbConnectionService.CreateConnection())
                using (var cmd = new SqlCommand("dbo.RetrieveAccessLevels", sqlConn))
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
        public async Task<string> AddNewUser(string unameIn, string pwdIn, int accIDIn)
        {
            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand("dbo.InsertLogin", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Username", unameIn);
                    cmd.Parameters.AddWithValue("@Password", LoginClass.EncryptPassword(pwdIn));
                    cmd.Parameters.AddWithValue("@AccID", accIDIn);
                    cmd.Parameters.Add("@status", SqlDbType.NVarChar, 4000);
                    cmd.Parameters["@status"].Direction = ParameterDirection.Output;
                    sqlConn.Open();
                    await cmd.ExecuteNonQueryAsync();
                    sqlConn.Close();
                    return (string)cmd.Parameters["@status"].Value;
                }
            }
            catch (SqlException sqlEx)
            {
                System.Diagnostics.Debug.WriteLine("SQL error: " + sqlEx.Message);
                return (string)sqlEx.Message;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("General error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack Trace: " + ex.StackTrace);
                return (string)ex.Message;
            }
        }
    }
}