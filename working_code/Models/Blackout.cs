using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MBTP.Models
{
    public class BlackoutDate
    {
        public int BlackoutID { get; set; }
        public int PCID { get; set; }
        public string ProfitCenterName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }
    }

     public class ProfitCenters
    {
        public int PCID { get; set; }
        public string Description { get; set; }

    }

    public class IncomeStatusInfoForBlackout
    {
        public int PCID { get; set; }
        public DateTime Date { get; set; }
        public Decimal Income { get; set; }
        public bool IsBlackout { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string DisplayText { get; set; }

    }

    public class LocationStatusSummary
    {
        public int PCID { get; set; }
        public string LocationName { get; set; }
        public string IsBlackoutedOut { get; set; }
        public string BlackoutReason { get; set; }
        public string ExpectedStatus { get; set; }
    }

}