using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MBTP.Interfaces;

namespace MBTP.Retrieval
{
    public class BookingRepository
    {
        private readonly IDatabaseConnectionService _dbConnectionService;
        public BookingRepository(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
        public DataSet GetBookingCountsByStateYearMonth(string state, int year, int month)
        {
            DataSet bookingDataSet = new DataSet();
            //Console.WriteLine($"Calling stored procedure for state: {state}, year: {year}, month: {month}");

            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand("dbo.GetBookingCountByStateYearMonth", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@StateIn", SqlDbType.NVarChar).Value = state;
                    cmd.Parameters.Add("@YearIn", SqlDbType.Int).Value = year;
                    cmd.Parameters.Add("@MonthIn", SqlDbType.Int).Value = month;

                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(cmd);
                    sqlConn.Open();
                    sqlDataAdapter.Fill(bookingDataSet);

                    if (bookingDataSet.Tables.Count > 0 && bookingDataSet.Tables[0].Rows.Count > 0)
                    {
                        //Console.WriteLine("Data Exists");
                    }
                    else
                    {
                        //Console.WriteLine("No Data");
                    }

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