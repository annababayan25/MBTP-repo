using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MBTP.Models{
    public class ExpressCheckinsClaimed
    {
        public int BookingId { get; set; }
        public DateTime ClaimedDate { get; set; }
        public string? DateString { get; set; }
    }
}