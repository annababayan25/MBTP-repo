using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MBTP.Models
{

    public class GLAccount
    {
        public string GlAccountId { get; set; }
        public string GlAccountCode { get; set; }
        public string GlAccountName { get; set; }
        public string LongDescription { get; set; }
        public string Refundable { get; set; }
        public string GlGroupId { get; set; }
        public string GlGroupName { get; set; }
        public string Active { get; set; }
    }
}