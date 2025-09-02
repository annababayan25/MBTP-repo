using System;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Configuration;
using System.Drawing.Text;
using MBTP.Controllers;
using MBTP.Interfaces;
using Microsoft.AspNetCore.Http;
namespace MBTP.Retrieval
{
    public class YearlyReport
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public YearlyReport(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }
    public DataSet GetYearly(string? fiscalYear, out DateTime startDate, out DateTime endDate, out bool isPositive, out decimal currentYearTotal)
        {
            DataSet currentYearData = new DataSet();
            isPositive = true;
            currentYearTotal = 0;

            try
            {
                // Dynamically calculate the fiscal year start and end dates
                if (!string.IsNullOrEmpty(fiscalYear) && int.TryParse(fiscalYear, out int fy))
                {
                    startDate = new DateTime(fy, 10, 1); // October 1 of the selected fiscal year
                    // determine the current fiscal year so we don't go past today's date if we are returning to current FY from prior FY
                    DateTime now = DateTime.Now;
                    if(startDate.AddYears(1) > now)
                    {
                        // if the selected FY end date is in the future, set it to today
                        endDate = now.AddDays(-1);
                    }
                    else
                    {
                        endDate = new DateTime(fy + 1, 9, 30); // September 30 of the selected fiscal year
                    }
                }
                else
                {
                    // Default to current fiscal year if no valid fiscal year is provided
                    DateTime now = DateTime.Now;
                    startDate = (now.Month >= 10)
                        ? new DateTime(now.Year, 10, 1) // October 1, current year
                        : new DateTime(now.Year - 1, 10, 1); // October 1, last year
                    // End date is yesterday to avoid partial data for today
                    endDate = now.AddDays(-1);
                }
                // Open database connection
                using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                {
                    sqlConn.Open();

                    // Fetch current fiscal year data
                    using (SqlCommand cmd = new SqlCommand("dbo.GetTotals", sqlConn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@StartDate", SqlDbType.Date).Value = startDate;
                        cmd.Parameters.Add("@EndDate", SqlDbType.Date).Value = endDate;
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
    public DataSet GetMonthlyBreakdownData(string? fiscalYear, out DateTime fiscalYearStartDate)
{
    DataSet allMonthlyData = new DataSet();

    try
    {
        using (SqlConnection sqlConn = _dbConnectionService.CreateConnection())
                {
                    sqlConn.Open();

                    // Dynamically calculate the fiscal year start and end dates
                    DateTime fiscalYearEndDate;
                    DateTime now = DateTime.Now;
                    if (!string.IsNullOrEmpty(fiscalYear) && int.TryParse(fiscalYear, out int fy))
                    {
                        fiscalYearStartDate = new DateTime(fy, 10, 1); // October 1 of the selected fiscal year
                                                             // determine the current fiscal year so we don't go past today's date if we are returning to current FY from prior FY
                        if (fiscalYearStartDate.AddYears(1) > now)
                        {
                            // if the selected FY end date is in the future, set it to today
                            fiscalYearEndDate = now.AddDays(-1);
                        }
                        else
                        {
                            fiscalYearEndDate = new DateTime(fy + 1, 9, 30); // September 30 of the selected fiscal year
                        }
                    }
                    else
                    {
                        // Default to current fiscal year if no valid fiscal year is provided
                        fiscalYearStartDate = (now.Month >= 10)
                            ? new DateTime(now.Year, 10, 1) // October 1, current year
                            : new DateTime(now.Year - 1, 10, 1); // October 1, last year
                                                                 // End date is yesterday to avoid partial data for today
                        fiscalYearEndDate = now.AddDays(-1);
                    }
                    DateTime startDate = fiscalYearStartDate;
                    DateTime endDate = startDate.AddMonths(1).AddDays(-1);

                    // Loop through months in the selected fiscal year, stopping at today's date if current FY
                    while (startDate <= fiscalYearEndDate)
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
    public DataSet GetDailyBreakdownData(string? fiscalYear, DateTime date, out DateTime fiscalYearStartDate)
{
    DataSet dailyData = new DataSet();
    try
    {
        // Dynamically calculate the month and fiscal start dates based on the provided date and fiscal year
        DateTime now = DateTime.Now;
        if (!string.IsNullOrEmpty(fiscalYear) && int.TryParse(fiscalYear, out int fy))
        {
            fiscalYearStartDate = new DateTime(fy, 10, 1); // October 1 of the selected fiscal year
            if(date.Month >= 10)
            {
                // if the selected date is in or after October, ensure the fiscal year matches
                date = new DateTime(fy, date.Month, date.Day); // Adjust date to the selected fiscal year
            }
            else 
            {
                // if the selected date is before October, it belongs to the next calendar year of the fiscal year
                date = new DateTime(fy + 1, date.Month, date.Day); // Adjust date to the selected fiscal year
            }
        }
        else
        {
            // Default to current fiscal year if no valid fiscal year is provided
            fiscalYearStartDate = (now.Month >= 10)
                ? new DateTime(now.Year, 10, 1) // October 1, current year
                : new DateTime(now.Year - 1, 10, 1); // October 1, last year
            date = (now.Month >= 10)
                ? new DateTime(now.Year, date.Month, date.Day) // Adjust date to current fiscal year
                : new DateTime(now.Year - 1, date.Month, date.Day); // Adjust date to last fiscal year
        }
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
