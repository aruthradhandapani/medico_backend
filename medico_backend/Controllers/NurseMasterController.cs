using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NurseMasterController : ControllerBase
    {
        private readonly NurseMasterClass _cls;

        public NurseMasterController(NurseMasterClass cls)
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

        // ─── Get by Ncode ─────────────────────────────────────────────
        [HttpGet("get-by-ncode")]
        public async Task<IActionResult> GetByNcode(int ncode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByNcode(ncode, tenant));
        }

        // ─── Get by Type ──────────────────────────────────────────────
        [HttpGet("get-by-type")]
        public async Task<IActionResult> GetByType(string ntype)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByType(ntype, tenant));
        }

        // ─── Search ───────────────────────────────────────────────────
        [HttpGet("search")]
        public async Task<IActionResult> Search(string key)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.Search(key, tenant));
        }

        // ─── Insert ───────────────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] NurseMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = "Success", ncode = data.ncode });
        }

        // ─── Update ───────────────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] NurseMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Update(data);
            return Ok(new { message = res });
        }

        // ─── Delete ───────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int ncode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var res = await _cls.Delete(ncode, tenant);
            if (res != "Success")
                return NotFound(new { message = res });

            return Ok(new { message = "Nurse deleted successfully" });
        }
    }
}