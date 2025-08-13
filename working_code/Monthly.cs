using System;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Configuration;
using System.Drawing.Text;
namespace MBTP.Retrieval
{
    public class MonthlyReport
    {
        private readonly IConfiguration _configuration;
        public MonthlyReport(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public DataSet GetMonthly(DateTime startDate, DateTime endDate, out bool isPositive, out decimal currentMonthTotal)
{
    DataSet currentMonthData = new DataSet();
    DataSet previousMonthData = new DataSet();
    isPositive = true;
    currentMonthTotal = 0;
    decimal previousMonthTotal = 0;

    try
    {   // Dave commented this block out 3/5/25
        // Adjust the startDate and endDate to align with the fiscal year
        //startDate = AdjustToFiscalYear(startDate);
        //endDate = AdjustToFiscalYear(endDate);
        using (SqlConnection sqlConn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        {
            sqlConn.Open();
            // Get data for the current month
            using (SqlCommand cmd = new SqlCommand("dbo.GetTotals", sqlConn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@StartDate", SqlDbType.Date).Value = startDate;
                cmd.Parameters.Add("@EndDate", SqlDbType.Date).Value = endDate;
                SqlDataAdapter myDA = new SqlDataAdapter(cmd); 
                myDA.Fill(currentMonthData);
            }

            // Calculate the previous month's fiscal year-adjusted range
            DateTime previousMonthStartDate = AdjustToFiscalYear(startDate.AddMonths(-1));
            DateTime previousMonthEndDate = AdjustToFiscalYear(endDate.AddMonths(-1));

            // Get data for the previous month
            using (SqlCommand cmd = new SqlCommand("dbo.GetTotals", sqlConn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@StartDate", SqlDbType.Date).Value = previousMonthStartDate;
                cmd.Parameters.Add("@EndDate", SqlDbType.Date).Value = previousMonthEndDate;
                SqlDataAdapter myDA = new SqlDataAdapter(cmd); 
                myDA.Fill(previousMonthData);
            }
        }

        // Calculate totals
        currentMonthTotal = ComputeTotal(currentMonthData);
        previousMonthTotal = ComputeTotal(previousMonthData);

        // Determine if the current month is positive compared to the previous month
        isPositive = currentMonthTotal >= previousMonthTotal;
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

    return currentMonthData;

    // Helper method to calculate the total
    decimal ComputeTotal(DataSet dataSet)
    {
        if (dataSet.Tables.Count > 0 && dataSet.Tables[0].Rows.Count > 0)
        {
            return dataSet.Tables[0].AsEnumerable().Sum(row => row.Field<decimal>("SiteTotal"));
        }
        return 0;
    }

    // Helper method to adjust to fiscal year logic
    DateTime AdjustToFiscalYear(DateTime date)
    {
        if (date.Month < 10) // Before October, part of the previous fiscal year
        {
            return new DateTime(date.Year - 1, date.Month, 1);
        }
        else // October or later, part of the current fiscal year
        {
            return new DateTime(date.Year, date.Month, 1);
        }
    }
}
    }
}
