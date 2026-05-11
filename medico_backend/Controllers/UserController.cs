using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserMasterController : ControllerBase
    {
        private readonly UserMasterClass cls;

        public UserMasterController(UserMasterClass _cls)
        {
            cls = _cls;
        }

        // ─────────────────────────────────────────
        // REGISTER
        // ─────────────────────────────────────────
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] UserMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            data.tenant_code = tenant;

            var res = await cls.Register(data);

            return Ok(res);
        }

        // ─────────────────────────────────────────
        // LOGIN
        // ─────────────────────────────────────────
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var res = await cls.Login(data, tenant);

            return Ok(res);
        }

        // ─────────────────────────────────────────
        // RESET PASSWORD
        // ─────────────────────────────────────────
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var res = await cls.ResetPassword(data, tenant);

            return Ok(res);
        }

        // ─────────────────────────────────────────
        // GET ALL USERS
        // ─────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.Get(tenant);

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // GET BY USERCODE
        // ─────────────────────────────────────────
        [HttpGet("get-by-usercode")]
        public async Task<IActionResult> GetByUserCode(int usercode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.GetByUserCode(usercode, tenant);

            if (data == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found"
                });
            }

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // DELETE USER
        // ─────────────────────────────────────────
        [HttpDelete("delete")]
        public async Task<IActionResult> Delete(int usercode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var res = await cls.Delete(usercode, tenant);

            return Ok(res);
        }
    }
}