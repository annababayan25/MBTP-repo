namespace MBTP.Models
{
    public class ReservationsDeposits
    {
        public DateTime DepositDate { get; set; }
        public decimal? Sites_Deposits_Taken { get; set; }
        public decimal? Sites_Deposits_Applied { get; set; }
        public decimal? Sites_Manual_Refunds { get; set; }
        public decimal? Sites_Net_Change { get; set; }
        public decimal? Rentals_Deposits_Taken { get; set; }
        public decimal? Rentals_Deposits_Applied { get; set; }
        public decimal? Rentals_Manual_Refunds { get; set; }
        public decimal? Rentals_Net_Change { get; set; }
        public decimal? Golf_Cart_Deposits_Taken { get; set; }
        public decimal? Golf_Cart_Deposits_Applied { get; set; }
        public decimal? Golf_Cart_Manual_Refunds { get; set; }
        public decimal? Golf_Cart_Net_Change { get; set; }
        public decimal? Gift_Vouchers_Purchased { get; set; }
        public decimal? Gift_Vouchers_Redeemed_For_Sites { get; set; }
        public decimal? Gift_Vouchers_Redeemed_For_Rentals { get; set; }
        public decimal? Gift_Vouchers_Redeemed_For_Storage { get; set; }
        public decimal? Gift_Vouchers_Redeemed_Net_Change { get; set; }
    }
}