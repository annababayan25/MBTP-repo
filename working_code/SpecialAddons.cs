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

namespace MBTP.Services
{
    public class SpecialAddonsService
    {
        private readonly IConfiguration _configuration;

        public SpecialAddonsService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string> UpdateAddons(int addIDin, DateTime dateIn, string glIn, string descIn, decimal amountIn)
        {
            try
            {
                using (SqlConnection sqlConn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("dbo.UpdateSpecialAddons", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@AddonID", addIDin);
                    cmd.Parameters.AddWithValue("@TransDate", dateIn.ToShortDateString());
                    cmd.Parameters.AddWithValue("@GLCode", glIn);
                    cmd.Parameters.AddWithValue("@Description", descIn);
                    cmd.Parameters.AddWithValue("@Amount", amountIn);
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
        public async Task<string> UpdateMiscFromAddons(DateTime dateIn, string glIn, string descIn, decimal amountIn)
        {
            try
            {
                using (SqlConnection sqlConn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (SqlCommand cmd = new SqlCommand("dbo.UpdateMiscTableFromAddons", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@TransDate", dateIn.ToShortDateString());
                    cmd.Parameters.AddWithValue("@GLCode", glIn);
                    cmd.Parameters.AddWithValue("@Description", descIn);
                    cmd.Parameters.AddWithValue("@Amount", amountIn);
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