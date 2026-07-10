using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymodeMasterController : ControllerBase
    {
        private readonly PaymodeMasterClass _cls;

        public PaymodeMasterController(PaymodeMasterClass cls)
        {
            _cls = cls;
        }

        // ─── Get All ──────────────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "tenant_code header is required" });

            return Ok(await _cls.Get(tenant));
        }

        // ─── Get by Pmcode ────────────────────────────────────────────
        [HttpGet("get-by-code")]
        public async Task<IActionResult> GetByCode(decimal pmcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "tenant_code header is required" });

            var res = await _cls.GetByCode(pmcode, tenant);
            if (res == null)
                return NotFound(new { message = "Payment mode not found" });

            return Ok(res);
        }

        // ─── Insert ───────────────────────────────────────────────────
        // pmcode does NOT need to be supplied in the body — it's generated
        // server-side, scoped per tenant (see PaymodeMasterClass.Insert).
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] PaymodeMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "tenant_code header is required" });

            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = "Success", pmcode = data.pmcode });
        }

        // ─── Update ───────────────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] PaymodeMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "tenant_code header is required" });

            data.tenant_code = tenant;

            var res = await _cls.Update(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = res });
        }

        // ─── Delete (soft) ────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(decimal pmcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "tenant_code header is required" });

            var res = await _cls.Delete(pmcode, tenant);
            if (res != "Success")
                return NotFound(new { message = res });

            return Ok(new { message = "Payment mode deleted successfully" });
        }

        // ─── Restore ──────────────────────────────────────────────────
        [HttpGet("restore")]
        public async Task<IActionResult> Restore(decimal pmcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "tenant_code header is required" });

            var res = await _cls.Restore(pmcode, tenant);
            if (res != "Success")
                return NotFound(new { message = res });

            return Ok(new { message = "Payment mode restored successfully" });
        }
    }
}