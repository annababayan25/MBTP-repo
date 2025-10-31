
using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Services
    {
        public class CheckedInListRepo
        {
            private readonly IDatabaseConnectionService _dbConnectionService;

            public CheckedInListRepo(IDatabaseConnectionService dbConnectionService)
            {
                _dbConnectionService = dbConnectionService;
            }

            public async Task SaveCheckedInListAsync(IEnumerable<CheckedIn> checkedInList)
            {
                using var sqlConn = _dbConnectionService.CreateConnection();
                await sqlConn.OpenAsync();

                foreach (var checkedIn in checkedInList)
                {
                    using (SqlCommand command = new SqlCommand("dbo.UpdateCheckedInTable", sqlConn))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@BookingId", checkedIn.BookingId);
                        command.Parameters.AddWithValue("@BookingName", (object?)checkedIn.BookingName ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Site", $"{checkedIn.CategoryName} {checkedIn.Site}" ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@BookingStatus", checkedIn.BookingStatus ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@CalculatedStayCost", checkedIn.CalculatedStayCost);
                        command.Parameters.AddWithValue("@DepositsHeld", checkedIn.DepositsHeld);
                        command.Parameters.AddWithValue("@LockFee", checkedIn.LockFee);
                        command.Parameters.AddWithValue("@SecurityDeposits", checkedIn.SecurityDeposits);
                        command.Parameters.AddWithValue("@OnlineBookingFee", checkedIn.OnlineBookingFee);
                        command.Parameters.AddWithValue("@PaymentsAfterCheckIn", checkedIn.PaymentsAfterCheckIn);
                        command.Parameters.AddWithValue("@PaymentsAfterCheckInDesc", checkedIn.PaymentsAfterCheckInDesc);
                        command.Parameters.AddWithValue("@Refunds", checkedIn.RefundedAmount);
                        command.Parameters.AddWithValue("@CancellationFee", checkedIn.CancellationFee);
                        command.Parameters.AddWithValue("@Extras", (object?)checkedIn.Extras ?? DBNull.Value);
                        command.Parameters.AddWithValue("@AccountBalance", checkedIn.AccountBalance == null ? (object)DBNull.Value : checkedIn.AccountBalance);
                        command.Parameters.AddWithValue("@BookingArrival", checkedIn.BookingArrival);
                        command.Parameters.AddWithValue("@BookingCheckedIn", checkedIn.BookingCheckedIn);
                        command.Parameters.AddWithValue("@BookingDeparture", checkedIn.BookingDeparture ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@CarLicensePlate", checkedIn.CarLicensePlate ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@CarLicensePlateExtra", checkedIn.CarLicensePlateExtra ?? (object)DBNull.Value);
                        command.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000).Direction = ParameterDirection.Output;
                        await command.ExecuteNonQueryAsync();
                    }
                }
                Console.WriteLine($"Total Checked-In: {checkedInList.Count()}");
                Console.WriteLine("Run method finished.");
            }
            
        }
    }
    
