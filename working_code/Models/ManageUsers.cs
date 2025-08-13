using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MBTP.Models
{
    public class AccessLevels
    {
        public int levelAccId { get; set; }
        public string? Description { get; set; }
    }
    public class NewUserData
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int AccID { get; set; }
    }
}