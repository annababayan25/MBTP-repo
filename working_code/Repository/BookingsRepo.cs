
using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Services
{
    public class BookingsRepo
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public BookingsRepo(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task SaveBookingsAsync(IEnumerable<Booking> bookingsList)
        {
            using var sqlConn = _dbConnectionService.CreateConnection();
            await sqlConn.OpenAsync();

            foreach (var booking in bookingsList)
            {
                using (SqlCommand command = new SqlCommand("dbo.UpdateBookingsTable", sqlConn))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BookingId", booking.BookingId);
                    command.Parameters.AddWithValue("@SiteName", booking.SiteName);
                    command.Parameters.AddWithValue("@BookingArrival", booking.BookingArrival);
                    command.Parameters.AddWithValue("@BookingDeparture", booking.BookingDeparture);
                    command.Parameters.AddWithValue("@BookingStatus", booking.BookingStatus ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@BookingAdults", booking.BookingAdults);
                    command.Parameters.AddWithValue("@BookingChildren", booking.BookingChildren);
                    command.Parameters.AddWithValue("@BookingInfants", booking.BookingInfants);
                    command.Parameters.AddWithValue("@BookingTotal", booking.BookingTotal);
                    command.Parameters.AddWithValue("@BookingMethodName", booking.BookingMethodName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@BookingSourceName", booking.BookingSourceName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@BookingReasonName", booking.BookingReasonName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@AccountBalance", booking.AccountBalance);
                    command.Parameters.AddWithValue("@BookingPlaced", booking.BookingPlaced);
                    command.Parameters.AddWithValue("@StateName", booking.StateName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CategoryName", booking.CategoryName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@BookingCancelled", booking.BookingCancelled ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@ExpressCheckin", booking.ExpressCheckin);
                    command.Parameters.AddWithValue("@StoredMBTP", booking.StoredMBTP ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@StoredOutside", booking.StoredOutside ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@EquipmentMake", booking.EquipmentMake ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@EquipmentModel", booking.EquipmentModel ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@EquipmentLength", booking.EquipmentLength ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@FirstName", booking.Firstname ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@LastName", booking.Lastname ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CarLicensePlate", booking.CarLicensePlate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CarLicensePlateExtra", booking.CarLicensePlateExtra ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@LockFee", booking.LockFee ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Wristbands", booking.Wristbands);
                    command.Parameters.Add("@status", SqlDbType.NVarChar, 4000);
                    command.Parameters["@status"].Direction = ParameterDirection.Output;
                    await command.ExecuteNonQueryAsync();
                    //Console.WriteLine(command.Parameters["@status"].Value.ToString());
                }
            }
            Console.WriteLine("Run method finished.");
        }
    }
}
