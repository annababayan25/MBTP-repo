namespace MBTP.Models
{
    public class ReservationsDeposits
    {
        public DateTime TransDate { get; set; }

        // =======================
        // Revenue
        // =======================
        public decimal? Sites { get; set; } = 0.0m;
        public decimal? Rentals { get; set; } = 0.0m;

        public decimal? Lock_Fees { get; set; } = 0.0m;
        public decimal? Extra_Vehicle_Fees { get; set; } = 0.0m;
        public decimal? Damage_Fees { get; set; } = 0.0m;
        public decimal? Late_Fees { get; set; } = 0.0m;

        public decimal? MRG1 { get; set; } = 0.0m;

        public decimal? Visitor_Fees { get; set; } = 0.0m;
        public decimal? Golf_Cart_Rentals { get; set; } = 0.0m;
        public decimal? MRGGolf { get; set; } = 0.0m;

        public decimal? Annual_Leases { get; set; } = 0.0m;
        public decimal? Employee { get; set; } = 0.0m;

        public decimal? LT_Sites { get; set; } = 0.0m;
        public decimal? LT_Rentals { get; set; } = 0.0m;
        public decimal? MH_Park { get; set; } = 0.0m;

        public decimal? MRG2 { get; set; } = 0.0m;

        public decimal? Storage { get; set; } = 0.0m;
        public decimal? Transfer_Fees { get; set; } = 0.0m;
        public decimal? Misc { get; set; } = 0.0m;
        public decimal? MRG3 { get; set; } = 0.0m;

        public decimal? Propane { get; set; } = 0.0m;
        public decimal? Events { get; set; } = 0.0m;
        public decimal? Supplemental { get; set; } = 0.0m;

        // =======================
        // Deposits
        // =======================
        public decimal? SiteDepTaken { get; set; } = 0.0m;
        public decimal? SiteDepTakenFuture { get; set; } = 0.0m;
        public decimal? SiteDepApp { get; set; } = 0.0m;
        public decimal? SiteDepMRG { get; set; } = 0.0m;

        public decimal? RentalDepTaken { get; set; } = 0.0m;
        public decimal? RentalDepTakenFuture { get; set; } = 0.0m;
        public decimal? RentalDepApp { get; set; } = 0.0m;
        public decimal? RentalDepMRG { get; set; } = 0.0m;

        public decimal? GolfDepTaken { get; set; } = 0.0m;
        public decimal? GolfDepTakenFuture { get; set; } = 0.0m;
        public decimal? GolfDepApp { get; set; } = 0.0m;
        public decimal? GolfDepMRG { get; set; } = 0.0m;

        // =======================
        // Vouchers
        // =======================
        public decimal? VouchersPurch { get; set; } = 0.0m;
        public decimal? VouchersRedSite { get; set; } = 0.0m;
        public decimal? VouchersRedRental { get; set; } = 0.0m;
        public decimal? VouchersRedStorage { get; set; } = 0.0m;
        public decimal? VouchersRedSiteDep { get; set; } = 0.0m;
        public decimal? VouchersRedRentalDep { get; set; } = 0.0m;

        // =======================
        // Payments
        // =======================
        public decimal? OfficeCC { get; set; } = 0.0m;
        public decimal? OfficeCash { get; set; } = 0.0m;

        // =======================
        // Transfers (T table)
        // =======================
        public decimal? CampsitesT { get; set; } = 0.0m;
        public decimal? RentalsT { get; set; } = 0.0m;
        public decimal? GolfCarts { get; set; } = 0.0m;
        public decimal? AnnualT { get; set; } = 0.0m;
        public decimal? MHParkT { get; set; } = 0.0m;
        public decimal? StorageT { get; set; } = 0.0m;
        public decimal? SiteDepositsT { get; set; } = 0.0m;
        public decimal? RentalDepositsT { get; set; } = 0.0m;
        public decimal? GolfDepositsT { get; set; } = 0.0m;

        public decimal? Vouchers { get; set; } = 0.0m;
        public decimal? Forfeits { get; set; } = 0.0m;
        public decimal? Guests { get; set; } = 0.0m;
        public decimal? Other { get; set; } = 0.0m;

        // =======================
        // Checks (C table)
        // =======================
        public decimal? CampsitesC { get; set; } = 0.0m;
        public decimal? RentalsC { get; set; } = 0.0m;
        public decimal? GolfC { get; set; } = 0.0m;
        public decimal? AnnualC { get; set; } = 0.0m;
        public decimal? LTCampsitesC { get; set; } = 0.0m;
        public decimal? LTRentalsC { get; set; } = 0.0m;
        public decimal? MHParkC { get; set; } = 0.0m;
        public decimal? StorageC { get; set; } = 0.0m;
        public decimal? SiteDepositsC { get; set; } = 0.0m;
        public decimal? RentalDepositsC { get; set; } = 0.0m;
        public decimal? GolfDepositsC { get; set; } = 0.0m;
        public decimal? OtherC { get; set; } = 0.0m;
    }
}
