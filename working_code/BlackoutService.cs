using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Retrieval
{
    public class BlackoutService
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public BlackoutService(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public List<BlackoutDate> ViewAllBlackoutDates()
        {
            var list = new List<BlackoutDate>();
            using (SqlConnection conn = _dbConnectionService.CreateConnection())
            using (SqlCommand cmd = new SqlCommand(@"SELECT b.BlackoutID, b.PCID, p.Description AS ProfitCenterName, b.StartDate, b.EndDate, b.Reason 
                                                    FROM BlackoutDates b
                                                    INNER JOIN ProfitCenters p on b.PCID = p.PCID
                                                    ORDER BY b.StartDate", conn))
            {
                conn.Open();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new BlackoutDate
                    {
                        BlackoutID = (int)reader["BlackoutID"],
                        PCID = (int)reader["PCID"],
                        ProfitCenterName = reader["ProfitCenterName"].ToString(),
                        StartDate = (DateTime)reader["StartDate"],
                        EndDate = (DateTime)reader["EndDate"],
                        Reason = reader["Reason"].ToString()
                    });
                }
                return list;
            }

        }

        public void InsertBlackoutDate(BlackoutDate blackout)
        {
            if (HasOverlap(blackout.PCID, blackout.StartDate, blackout.EndDate))
                throw new InvalidOperationException("This blackout period overlaps with an existing blackout");
            try
                {
                    using (SqlConnection conn = _dbConnectionService.CreateConnection())
                    using (SqlCommand cmd = new SqlCommand(@"INSERT INTO BlackoutDates (PCID, StartDate, EndDate, Reason)
                                                             VALUES (@PCID, @StartDate, @EndDate, @Reason)", conn))
                    {
                        cmd.Parameters.AddWithValue("@PCID", blackout.PCID);
                        cmd.Parameters.AddWithValue("@StartDate", blackout.StartDate);
                        cmd.Parameters.AddWithValue("@EndDate", blackout.EndDate);
                        cmd.Parameters.AddWithValue("@Reason", blackout.Reason ?? "");

                        conn.Open();
                        int result = cmd.ExecuteNonQuery();

                        if (result == 0)
                            throw new InvalidOperationException("Failed to insert blackout date - 0 rows affected");

                        System.Diagnostics.Debug.WriteLine($"Successly added blackout: PCID={blackout.PCID}, StartDate={blackout.StartDate}, EndDate={blackout.EndDate}");
                    }
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

        public void UpdateBlackoutDate(BlackoutDate blackout)
        {
            try
            {
                using (SqlConnection conn = _dbConnectionService.CreateConnection())
                using (SqlCommand cmd = new SqlCommand(@"UPDATE BlackoutDates
                                                         SET StartDate = @StartDate,
                                                         EndDate = @EndDate,
                                                         Reason = @Reason
                                                         WHERE BlackoutID = @BlackoutID", conn))
                {
                    cmd.Parameters.AddWithValue("@BlackoutID", blackout.BlackoutID);
                    cmd.Parameters.AddWithValue("@PCID", blackout.PCID);
                    cmd.Parameters.AddWithValue("@StartDate", blackout.StartDate);
                    cmd.Parameters.AddWithValue("@EndDate", blackout.EndDate);
                    cmd.Parameters.AddWithValue("@Reason", blackout.Reason ?? "");

                    conn.Open();
                    int result = cmd.ExecuteNonQuery();

                    System.Diagnostics.Debug.WriteLine($"Successly updated blackout: PCID={blackout.PCID}, StartDate={blackout.StartDate}, EndDate={blackout.EndDate}");
                }
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

        public void DeleteBlackoutDate(int blackoutID)
        {
            using (SqlConnection conn = _dbConnectionService.CreateConnection())
            using (SqlCommand cmd = new SqlCommand(@"DELETE FROM BlackoutDAtes WHERE BlackoutID = @BlackoutID", conn))
            {
                cmd.Parameters.AddWithValue("@BlackoutID", blackoutID);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public bool IsBlackout(int PCID, DateTime date)
        {
            using (SqlConnection conn = _dbConnectionService.CreateConnection())
            using (SqlCommand cmd = new SqlCommand(@"SELECT COUNT(*) FROM BlackoutDates WHERE PCID = @PCID AND StartDate <= @Date AND EndDate >= @Date", conn))
            {
            
                cmd.Parameters.AddWithValue("@PCID", PCID);
                cmd.Parameters.AddWithValue("@Date", date);

                conn.Open();
                int count = (int)cmd.ExecuteScalar();
                return count > 0;
            }
        }

        public bool HasOverlap(int PCID, DateTime startDate, DateTime endDate, int? excludeId = null)
        {
            string sql = @"
                SELECT COUNT(*) 
                FROM BlackoutDates 
                WHERE PCID = @PCID
                AND StartDate <= @EndDate 
                AND EndDate >= @StartDate";

            if (excludeId.HasValue)
                sql += " AND BlackoutID != @BlackoutID";

            using (SqlConnection conn = _dbConnectionService.CreateConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@PCID", PCID);
                cmd.Parameters.AddWithValue("@StartDate", startDate);
                cmd.Parameters.AddWithValue("@EndDate", endDate);
                if (excludeId.HasValue) cmd.Parameters.AddWithValue("@BlackoutID", excludeId);

                conn.Open();
                int count = (int)cmd.ExecuteScalar();
                return count > 0;
            }
        }


        public List<ProfitCenters> GetAllProfitCenters()
        {
            var operations = new List<ProfitCenters>();
            using (SqlConnection conn = _dbConnectionService.CreateConnection())
            using (SqlCommand cmd = new SqlCommand(@"SELECT * FROM ProfitCenters ORDER BY PCID", conn))
            {
                conn.Open();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    operations.Add(new ProfitCenters
                    {
                        PCID = (int)reader["PCID"],
                        Description = reader["Description"].ToString()
                    });
                }
            }
            return operations;
        }


    }
}
    