namespace MBTP.Models
{
    public class ReservationsDeposits
    {
        public DateTime TransDate { get; set; }

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

        public decimal? VouchersPurch { get; set; } = 0.0m;
        public decimal? VouchersRedSite { get; set; } = 0.0m;
        public decimal? VouchersRedRental { get; set; } = 0.0m;
        public decimal? VouchersRedStorage { get; set; } = 0.0m;
        public decimal? VouchersRedSiteDep { get; set; } = 0.0m;
        public decimal? VouchersRedRentalDep { get; set; } = 0.0m;
    }
}
