
using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Services
    {
        public class DepositsHeldRepo
        {
            private readonly IDatabaseConnectionService _dbConnectionService;

            public DepositsHeldRepo(IDatabaseConnectionService dbConnectionService)
            {
                _dbConnectionService = dbConnectionService;
            }

            public async Task SaveDepositsHeldAsync(IEnumerable<ReservationsDeposits> deposits)
            {
                using var sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                foreach (var d in deposits)
                {

                    using (SqlCommand cmd = new SqlCommand(@"dbo.UpdateReservationsDepositsTable", sqlConn))
                    {
                    cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@TransDate", d.DepositDate);
                        cmd.Parameters.AddWithValue("@Sites_Deposits_Taken", d.Sites_Deposits_Taken ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Sites_Deposits_Applied", d.Sites_Deposits_Applied ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Sites_Manual_Refunds", d.Sites_Manual_Refunds ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Rentals_Deposits_Taken", d.Rentals_Deposits_Taken ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Rentals_Deposits_Applied", d.Rentals_Deposits_Applied ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Rentals_Manual_Refunds", d.Rentals_Manual_Refunds ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Golf_Cart_Deposits_Taken", d.Golf_Cart_Deposits_Taken ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Golf_Cart_Deposits_Applied", d.Golf_Cart_Deposits_Applied ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Golf_Cart_Manual_Refunds", d.Golf_Cart_Manual_Refunds ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Gift_Vouchers_Purchased", d.Gift_Vouchers_Purchased ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Gift_Vouchers_Redeemed_For_Sites", d.Gift_Vouchers_Redeemed_For_Sites ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Gift_Vouchers_Redeemed_For_Rentals", d.Gift_Vouchers_Redeemed_For_Rentals ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Gift_Vouchers_Redeemed_For_Storage", d.Gift_Vouchers_Redeemed_For_Storage ?? (object)DBNull.Value);
                        cmd.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }
