
using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Services
    {
        public class TransactionFlowRepo
        {
            private readonly IDatabaseConnectionService _dbConnectionService;

            public TransactionFlowRepo(IDatabaseConnectionService dbConnectionService)
            {
                _dbConnectionService = dbConnectionService;
            }

            public async Task SaveTransactionFlowsAsync(IEnumerable<TransactionFlow> transactions)
            {
                using var sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                foreach (var t in transactions)
                {
                    var dateValue = Convert.ToDateTime(t.TransDate);
                    var stringValue = dateValue.ToString("MMM dd yyyy hh:mm tt");

                    using (SqlCommand cmd = new SqlCommand(@"dbo.UpdateTransactionFlowTable", sqlConn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@PaymentMethod", $"{t.PaymentMethod} {t.TranslatedPaymentType} {t.PaymentTypeAction} - For {Convert.ToDateTime(t.TransDate).ToString("MMM dd yyyy")}");
                        cmd.Parameters.AddWithValue("@Category", t.Category ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@TransNumber", t.PaymentTypeReference != null ? $"{t.TransType} #{t.ItemId} (Ref #{t.PaymentTypeReference})" : $"{t.TransType} #{t.ItemId}");
                        cmd.Parameters.AddWithValue("@TransDate", stringValue);
                        cmd.Parameters.AddWithValue("@ClientAccount", t.ClientAccount);
                        cmd.Parameters.AddWithValue("@GeneratedBy", t.GeneratedBy);
                        cmd.Parameters.AddWithValue("@Description", t.Description);
                        cmd.Parameters.AddWithValue("@Amount", t.Amount ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ArrivalDate", t.ArrivalDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DepartureDate", t.DepartureDate ?? (object)DBNull.Value);
                        cmd.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }
