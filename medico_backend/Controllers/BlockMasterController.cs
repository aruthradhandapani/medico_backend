using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlockMasterController : ControllerBase
    {
        private readonly BlockMasterClass _cls;

        public BlockMasterController(BlockMasterClass cls)
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

        // ─── Get by Blockcode ─────────────────────────────────────────
        [HttpGet("get-by-blockcode")]
        public async Task<IActionResult> GetByBlockcode(int blockcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByBlockcode(blockcode, tenant));
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
        public async Task<IActionResult> Insert([FromBody] BlockMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = "Success", blockcode = data.blockcode });
        }

        // ─── Update ───────────────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] BlockMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Update(data);
            return Ok(new { message = res });
        }

        // ─── Delete ───────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int blockcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var res = await _cls.Delete(blockcode, tenant);
            if (res != "Success")
                return NotFound(new { message = res });

            return Ok(new { message = "Block deleted successfully" });
        }
    }
}