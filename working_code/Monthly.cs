using System;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using MBTP.Interfaces;

namespace MBTP.Retrieval
{
    public class MonthlyReport
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public MonthlyReport(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public DataSet GetMonthly(string? fiscalYear, ref DateTime startDate, ref DateTime endDate, out bool isPositive, out decimal currentMonthTotal)
        {
            DataSet currentMonthData = new DataSet();
            DataSet previousMonthData = new DataSet();
            isPositive = true;
            currentMonthTotal = 0;
            decimal previousMonthTotal = 0;

            try
            {
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                {
                    if (!string.IsNullOrEmpty(fiscalYear) && int.TryParse(fiscalYear, out int fy))
                    {
                        startDate = (startDate.Month >= 10)
                            ? new DateTime(fy, startDate.Month, 1) // use the fiscal year as the calendar year if on or after October
                            : new DateTime(fy + 1, startDate.Month, 1); // add one to the fiscal year for the correct calendar year
                        endDate = new DateTime(startDate.Year, endDate.Month, endDate.Day); // ensure the end date is in the same calendar year as the start date
                    }
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

                // Commented out 9/2/25 by Dave because this comparison is incomplete, only relying on Site Totals. It's also deceptive
                // because certain months will always be lower than others due to seasonality.
                // Determine if the current month is positive compared to the previous month
                //isPositive = currentMonthTotal >= previousMonthTotal;
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
        }

        private decimal ComputeTotal(DataSet dataSet)
        {
            if (dataSet.Tables.Count > 0 && dataSet.Tables[0].Rows.Count > 0)
            {
                return dataSet.Tables[0].AsEnumerable().Sum(row => row.Field<decimal>("SiteTotal"));
            }
            return 0;
        }

        private DateTime AdjustToFiscalYear(DateTime date)
        {
            return date.Month < 10
                ? new DateTime(date.Year - 1, date.Month, 1)
                : new DateTime(date.Year, date.Month, 1);
        }
    }
}
