using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentBotController : ControllerBase
    {
        private readonly AppointmentBotClass _cls;

        public AppointmentBotController(AppointmentBotClass cls)
        {
            _cls = cls;
        }

        // ─── Get All ──────────────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.Get(tenant));
        }

        // ─── Get by Bot ID ────────────────────────────────────────────
        [HttpGet("get-by-botid")]
        public async Task<IActionResult> GetByBotId(int bot_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByBotId(bot_id, tenant));
        }

        // ─── Get by Bh_code ───────────────────────────────────────────
        [HttpGet("get-by-bhcode")]
        public async Task<IActionResult> GetByBhCode(int bh_code)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByBhCode(bh_code, tenant));
        }

        // ─── Insert ───────────────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] AppointmentBotModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = "Success", bot_id = data.bot_id });
        }

        // ─── Update ───────────────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] AppointmentBotModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Update(data);
            return Ok(new { message = res });
        }

        // ─── Delete ───────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int bot_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var res = await _cls.Delete(bot_id, tenant);
            if (res != "Success")
                return NotFound(new { message = res });

            return Ok(new { message = "Bot config deleted successfully" });
        }
    }
}