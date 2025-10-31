namespace MBTP.Models
{
    public class ReservationsDeposits
    {
        public DateTime DepositDate { get; set; }
        public decimal? Sites { get; set; } // F and R: Sites_Deposits_Applied + Payments After Check in 
        public decimal? Mobile_Home_Rentals { get; set; }
        public decimal? Rentals { get; set; } // F and R: Rentals_Deposits_Applied + Payments After Check in 
        public decimal? Locks_Total { get; set; } // Total Lock Fee Payments
        public decimal? Damage_Fees { get; set; }
        public decimal? Extra_Fees { get; set; }
        public decimal? Extra_Vehicles { get; set; }
        public decimal? Late_Fees { get; set; }
        public decimal? Manual_Refunds { get; set; }
        public decimal? Visitor_Fees { get; set; }
        public decimal? Annual_Total { get; set; }
        public decimal? Employee { get; set; }
        public decimal? LT_Sites { get; set; } // Lease Transfer Sites
        public decimal? LT_Rentals { get; set; } // Lease Transfer Rentals
        public decimal? MH_Park { get; set; } // Mobile Home Parks
        public decimal? Manual_Refunds_NT { get; set; } // Manual Refunds in DailyBreakdownF
        public decimal? Storage { get; set; }
        public decimal? Transfer_Fees { get; set; }
        public decimal? Misc { get; set; }
        public decimal? Propane { get; set; }
        public decimal? Manual_Refunds_NT2 { get; set; } 
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