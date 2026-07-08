using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WardMasterController : ControllerBase
    {
        private readonly WardMasterClass _cls;

        public WardMasterController(WardMasterClass cls)
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

        // ─── Get by Wrdcode ───────────────────────────────────────────
        [HttpGet("get-by-wrdcode")]
        public async Task<IActionResult> GetByWrdcode(int wrdcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByWrdcode(wrdcode, tenant));
        }

        // ─── Get by Branch ────────────────────────────────────────────
        [HttpGet("get-by-branch")]
        public async Task<IActionResult> GetByBranch(int branchcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByBranch(branchcode, tenant));
        }

        // ─── Insert ───────────────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] WardMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = "Success", wrdcode = data.wrdcode });
        }

        // ─── Update ───────────────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] WardMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Update(data);
            return Ok(new { message = res });
        }

        // ─── Delete ───────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int wrdcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var res = await _cls.Delete(wrdcode, tenant);
            if (res != "Success")
                return NotFound(new { message = res });

            return Ok(new { message = "Ward deleted successfully" });
        }
    }
}