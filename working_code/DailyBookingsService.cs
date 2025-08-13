using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Retrieval
{
    public class DailyBookingsService
    {
        private readonly IDatabaseConnectionService _dbConnectionService;
        public DailyBookingsService(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
        public DataSet GetBookingsDataset(DateTime periodFrom, DateTime periodTo)
        {
            DataSet bookingDataSet = new DataSet();
            //Console.WriteLine($"Calling stored procedure for state: {state}, year: {year}, month: {month}");

            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new("dbo.RetrieveDailyBookings", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@startDate", SqlDbType.Date).Value = periodFrom;
                    cmd.Parameters.Add("@EndDate", SqlDbType.Date).Value = periodTo;

                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    sqlDataAdapter.Fill(bookingDataSet);

                    sqlConn.Close();
                }
                return bookingDataSet;
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine("SQL error: " + sqlEx.Message);
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("General error: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);
                throw;
            }
        }

    }
}