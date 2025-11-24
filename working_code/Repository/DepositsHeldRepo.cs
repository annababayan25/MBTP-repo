using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Services
{
    public class ReservationsDepositsRepo
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public ReservationsDepositsRepo(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task SaveReservationsDepositsAsync(IEnumerable<ReservationsDeposits> items)
        {
            using var sqlConn = _dbConnectionService.CreateConnection();
            await sqlConn.OpenAsync();

            foreach (var d in items)
            {
                using (SqlCommand cmd = new SqlCommand(@"dbo.UpdateFrontOfficeTransactions", sqlConn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@TransDate", d.TransDate);

                    // Deposits
                    cmd.Parameters.AddWithValue("@SiteDepTaken", d.SiteDepTaken ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@SiteDepTakenFuture", d.SiteDepTakenFuture ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@SiteDepApp", d.SiteDepApp ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@SiteDepMRG", d.SiteDepMRG ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@RentalDepTaken", d.RentalDepTaken ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@RentalDepTakenFuture", d.RentalDepTakenFuture ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@RentalDepApp", d.RentalDepApp ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@RentalDepMRG", d.RentalDepMRG ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@GolfDepTaken", d.GolfDepTaken ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@GolfDepTakenFuture", d.GolfDepTakenFuture ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@GolfDepApp", d.GolfDepApp ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@GolfDepMRG", d.GolfDepMRG ?? (object)DBNull.Value);

                    // Vouchers
                    cmd.Parameters.AddWithValue("@VouchersPurch", d.VouchersPurch ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@VouchersRedSite", d.VouchersRedSite ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@VouchersRedRental", d.VouchersRedRental ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@VouchersRedStorage", d.VouchersRedStorage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@VouchersRedSiteDep", d.VouchersRedSiteDep ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@VouchersRedRentalDep", d.VouchersRedRentalDep ?? (object)DBNull.Value);

                    // Regular revenue
                    cmd.Parameters.AddWithValue("@Sites", d.Sites ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Rentals", d.Rentals ?? (object)DBNull.Value);

                    // Fees
                    cmd.Parameters.AddWithValue("@LockFees", d.Lock_Fees ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ExtraVehicleFees", d.Extra_Vehicle_Fees ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DamageFees", d.Damage_Fees ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LateFees", d.Late_Fees ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@MRG1", d.MRG1 ?? (object)DBNull.Value);

                    // Misc revenue
                    cmd.Parameters.AddWithValue("@VisitorFees", d.Visitor_Fees ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@GolfCartRentals", d.GolfDepApp ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MRGGolf", d.GolfDepMRG ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@AnnualLeases", d.Annual_Leases ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Employee", d.Employee ?? (object)DBNull.Value);

                    // Long Term
                    cmd.Parameters.AddWithValue("@LTSites", d.LT_Sites ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LTRentals", d.LT_Rentals ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MHPark", d.MH_Park ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@MRG2", d.MRG2 ?? (object)DBNull.Value);

                    // More misc categories
                    cmd.Parameters.AddWithValue("@Storage", d.Storage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TransferFees", d.Transfer_Fees ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Misc", d.Misc ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MRG3", d.MRG3 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Propane", d.Propane ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Events", d.Events ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Supplemental", d.Supplemental ?? (object)DBNull.Value);


                    cmd.Parameters.Add("@ProcStatus", SqlDbType.NVarChar, 4000)
                        .Direction = ParameterDirection.Output;

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
