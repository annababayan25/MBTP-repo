using System;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Configuration;
using System.Drawing.Text;
using MBTP.Controllers;
using MBTP.Interfaces;

namespace MBTP.Retrieval
{
    public class YearlyReport
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public YearlyReport(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
    public DataSet GetYearly(DateTime? startDate, DateTime? endDate, out bool isPositive, out decimal currentYearTotal)
        {
            DataSet currentYearData = new DataSet();
            isPositive = true;
            currentYearTotal = 0;

            try
            {
                // Dynamically calculate the fiscal year start and end if not provided
                DateTime now = DateTime.Now;

                // Determine fiscal year start dynamically
                if (!startDate.HasValue || !endDate.HasValue)
                {
                    // Ensure startDate aligns with the current fiscal year
                    startDate = (now.Month >= 10)
                        ? new DateTime(now.Year, 10, 1) // October 1, current year
                        : new DateTime(now.Year - 1, 10, 1); // October 1, last year

                    // End date is the current date
                    endDate = now;
                }

                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                {
                    sqlConn.Open();

                    // Fetch current fiscal year data
                    using (SqlCommand cmd = new SqlCommand("dbo.GetTotals", sqlConn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@StartDate", SqlDbType.Date).Value = startDate.Value;
                        cmd.Parameters.Add("@EndDate", SqlDbType.Date).Value = endDate.Value;
                        SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                        myDA.Fill(currentYearData);
                    }
                }

                // Calculate total for the current fiscal year
                currentYearTotal = ComputeTotal(currentYearData);
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

            return currentYearData;

            // Helper method to compute total
            decimal ComputeTotal(DataSet dataSet)
            {
                if (dataSet.Tables.Count > 0 && dataSet.Tables[0].Rows.Count > 0)
                {
                    return dataSet.Tables[0].AsEnumerable().Sum(row => row.Field<decimal>("SiteTotal"));
                }
                return 0;
            }
        }
    public DataSet GetMonthlyBreakdownData(DateTime fiscalYearStartDate)
{
    // Force fiscalYearStartDate to always align with the current fiscal year
    DateTime now = DateTime.Now;
    fiscalYearStartDate = (now.Month >= 10)
        ? new DateTime(now.Year, 10, 1)  // Current fiscal year's October
        : new DateTime(now.Year - 1, 10, 1); // Last fiscal year's October

    DataSet allMonthlyData = new DataSet();

    try
    {
        using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                {
                    sqlConn.Open();

                    // Calculate the end of the fiscal year
                    DateTime fiscalYearEndDate = fiscalYearStartDate.AddMonths(12).AddDays(-1); // End of fiscal year
                    DateTime startDate = fiscalYearStartDate;
                    DateTime endDate = startDate.AddMonths(1).AddDays(-1);

                    // Loop through months in the current fiscal year, stopping at today's date
                    while (startDate <= fiscalYearEndDate && startDate <= now)
                    {
                        DataSet monthlyData = new DataSet();
                        using (SqlCommand cmd = new SqlCommand("dbo.GetTotals", sqlConn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("@StartDate", SqlDbType.Date).Value = startDate;
                            cmd.Parameters.Add("@EndDate", SqlDbType.Date).Value = endDate;

                            SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                            myDA.Fill(monthlyData);

                            // Merge monthly data into the cumulative dataset
                            allMonthlyData.Merge(monthlyData);
                        }

                        // Move to the next month
                        startDate = startDate.AddMonths(1);
                        endDate = startDate.AddMonths(1).AddDays(-1);
                    }
                }
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

    return allMonthlyData;
}
    public DataSet GetDailyBreakdownData(DateTime date, DateTime fiscalYearStartDate)
{
    DataSet dailyData = new DataSet();
    try
    {
        using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
        {
            sqlConn.Open();
            using (SqlCommand cmd = new SqlCommand("dbo.GetDailyTotalsDave", sqlConn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add("@Date", SqlDbType.Date).Value = date;
                SqlDataAdapter myDA = new SqlDataAdapter(cmd);
                SqlDataAdapter myDA2 = new SqlDataAdapter(cmd);
                SqlDataAdapter myDA3 = new SqlDataAdapter(cmd);
                SqlDataAdapter myDA4 = new SqlDataAdapter(cmd);
                SqlDataAdapter myDA5 = new SqlDataAdapter(cmd);
                myDA.Fill(dailyData);
                cmd.Parameters["@Date"].Value = date.AddYears(-1);
                myDA2 = new SqlDataAdapter(cmd);
                myDA2.Fill(dailyData, "Prior");
                cmd.Parameters["@Date"].Value = date.AddYears(-2);
                myDA3 = new SqlDataAdapter(cmd);
                myDA3.Fill(dailyData, "Prior2");
                // now get YTD totals for help with full month projections
                DateTime fiscalYearStartDate2 = (date.Month >= 10)
                    ? new DateTime(date.Year, 10, 1)  // Current fiscal year's October
                    : new DateTime(date.Year - 1, 10, 1); // Last fiscal year's October
                cmd.Parameters.RemoveAt(0);
                cmd.CommandText = "dbo.GetTotals";
                cmd.Parameters.Add("@StartDate", SqlDbType.Date).Value = fiscalYearStartDate;
                cmd.Parameters.Add("@EndDate", SqlDbType.Date).Value = date.AddDays(-1);
                myDA4 = new SqlDataAdapter(cmd);
                myDA4.Fill(dailyData, "YTD");
                cmd.CommandText = "dbo.RetrieveBlackoutState";
                DateTime blackoutStart = new DateTime(date.Year, date.Month, 1);
                DateTime blackoutEnd = blackoutStart.AddMonths(1).AddDays(-1);
                cmd.Parameters.Clear();
                cmd.Parameters.Add("@StartDate", SqlDbType.Date).Value = blackoutStart;
                cmd.Parameters.Add("@EndDate", SqlDbType.Date).Value = blackoutEnd;
                myDA5 = new SqlDataAdapter(cmd);
                myDA5.Fill(dailyData, "Blackout");
            }
        }
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
    return dailyData;
}
    }
}
