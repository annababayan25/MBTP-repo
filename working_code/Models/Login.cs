using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MBTP.Pages
{
    public class LoginModel : PageModel
    {
        public List<DataTable> Tables { get; set; } = new List<DataTable>();
        public int AccID {get;set;}


    }
}