using System.Data;
using Microsoft.Data.SqlClient;
using MBTP.Models;
using MBTP.Interfaces;

namespace MBTP.Services
{
    public class ReservationsRepo
    {
        private readonly IDatabaseConnectionService _dbConnectionService;

        public ReservationsRepo(IDatabaseConnectionService dbConnectionService)
        {
            _dbConnectionService = dbConnectionService;
        }

        public async Task SaveReservationsAsync(IEnumerable<Reservations> items)
    {
        using var sqlConn = _dbConnectionService.CreateConnection();
        await sqlConn.OpenAsync();

        foreach (var d in items)
        {
            using SqlCommand cmd = new SqlCommand("dbo.UpdateFrontOfficeTransactions", sqlConn);
            cmd.CommandType = CommandType.StoredProcedure;

            // === Base ===
            cmd.Parameters.AddWithValue("@TransDate", d.TransDate);

            // === Revenue ===
            cmd.Parameters.AddWithValue("@Sites", d.Sites ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Rentals", d.Rentals ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LockFees", d.Lock_Fees ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ExtraVehicleFees", d.Extra_Vehicle_Fees ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DamageFees", d.Damage_Fees ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LateFees", d.Late_Fees ?? (object)DBNull.Value);

            cmd.Parameters.AddWithValue("@MRG1", d.MRG1 ?? (object)DBNull.Value);

            cmd.Parameters.AddWithValue("@VisitorFees", d.Visitor_Fees ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GolfCartRentals", d.Golf_Cart_Rentals ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MRGGolf", d.GolfDepMRG ?? (object)DBNull.Value);

            cmd.Parameters.AddWithValue("@Annual", d.Annual_Leases ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Employee", d.Employee ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LTSites", d.LT_Sites ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LTUnits", d.LT_Rentals ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MHPark", d.MH_Park ?? (object)DBNull.Value);

            cmd.Parameters.AddWithValue("@MRG2", d.MRG2 ?? (object)DBNull.Value);

            cmd.Parameters.AddWithValue("@Storage", d.Storage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TransferFees", d.Transfer_Fees ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Misc", d.Misc ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MRG3", d.MRG3 ?? (object)DBNull.Value);

            cmd.Parameters.AddWithValue("@Propane", d.Propane ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Events", d.Events ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Supplemental", d.Supplemental ?? (object)DBNull.Value);

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

            cmd.Parameters.AddWithValue("@VouchersPurch", d.VouchersPurch ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@VouchersRedSite", d.VouchersRedSite ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@VouchersRedRental", d.VouchersRedRental ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@VouchersRedStorage", d.VouchersRedStorage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@VouchersRedSiteDep", d.VouchersRedSiteDep ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@VouchersRedRentalDep", d.VouchersRedRentalDep ?? (object)DBNull.Value);

            cmd.Parameters.AddWithValue("@OfficeCC", d.OfficeCC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OfficeCash", d.OfficeCash ?? (object)DBNull.Value);

            cmd.Parameters.AddWithValue("@CampsitesT", d.CampsitesT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RentalsT", d.RentalsT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GolfCarts", d.GolfCarts ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AnnualT", d.AnnualT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MHParkT", d.MHParkT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@StorageT", d.StorageT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SiteDepositsT", d.SiteDepositsT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RentalDepositsT", d.RentalDepositsT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GolfDepositsT", d.GolfDepositsT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Vouchers", d.Vouchers ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Forfeits", d.Forfeits ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Guests", d.Guests ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Other", d.Other ?? (object)DBNull.Value);

            cmd.Parameters.AddWithValue("@CampsitesC", d.CampsitesC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RentalsC", d.RentalsC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GolfC", d.GolfC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LTCampsitesC", d.LTCampsitesC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LTRentalsC", d.LTRentalsC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AnnualC", d.AnnualC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MHParkC", d.MHParkC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@StorageC", d.StorageC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SiteDepositsC", d.SiteDepositsC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RentalDepositsC", d.RentalDepositsC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GolfDepositsC", d.GolfDepositsC ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OtherC", d.OtherC ?? (object)DBNull.Value);

            cmd.Parameters.Add("@status", SqlDbType.NVarChar, 4000)
                .Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();

            string result = (string)cmd.Parameters["@status"].Value;
            if (result != "SUCCESS")
            {
                throw new Exception($"Stored procedure failed: {result}");
            }
        }
    }
}}