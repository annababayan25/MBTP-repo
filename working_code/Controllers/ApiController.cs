using Microsoft.AspNetCore.Mvc;
using MBTP.Services;
using System.Threading.Tasks;

namespace MBTP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GLAccountsController : ControllerBase
    {
        private readonly GLAccountApi _glAccountApi;

        public GLAccountsController(GLAccountApi glAccountApi)
        {
            _glAccountApi = glAccountApi;
        }

        // GET: api/glaccounts
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var accounts = await _glAccountApi.FetchAllGLAccounts();
            return Ok(accounts); // returns JSON
        }
    }
}
