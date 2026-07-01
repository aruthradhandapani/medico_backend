using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MachineMasterController : ControllerBase
    {
        private readonly MachineMasterClass _cls;

        public MachineMasterController(MachineMasterClass cls)
        {
            _cls = cls;
        }

        // ─── Get All ──────────────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code required" });

            var result = await _cls.Get(tenant);
            return Ok(result);
        }

        // ─── Get by Mccode ────────────────────────────────────────────
        [HttpGet("get-by-mccode")]
        public async Task<IActionResult> GetByMccode([FromQuery] int mccode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code required" });

            var result = await _cls.GetByMccode(mccode, tenant);
            if (result == null)
                return NotFound(new { message = "Machine not found" });

            return Ok(result);
        }

        // ─── Get Next Mccode ──────────────────────────────────────────
        [HttpGet("get-next-mccode")]
        public async Task<IActionResult> GetNextMccode()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code required" });

            var result = await _cls.GetNextMccode(tenant);
            return Ok(result);
        }

        // ─── Insert ───────────────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] MachineMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code required" });

            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            return res == "Success"
                ? Ok(new { message = "Success", mccode = data.mccode })
                : BadRequest(new { message = res });
        }

        // ─── Update ───────────────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] MachineMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code required" });

            data.tenant_code = tenant;

            var res = await _cls.Update(data);
            return res == "Success"
                ? Ok(new { message = "Updated successfully" })
                : BadRequest(new { message = res });
        }

        // ─── Delete ───────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete([FromQuery] int mccode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code required" });

            var res = await _cls.Delete(mccode, tenant);
            return res == "Success"
                ? Ok(new { message = "Deleted successfully" })
                : BadRequest(new { message = res });
        }
    }
}