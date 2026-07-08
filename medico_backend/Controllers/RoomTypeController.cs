using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoomTypeMasterController : ControllerBase
    {
        private readonly RoomTypeMasterClass _cls;

        public RoomTypeMasterController(RoomTypeMasterClass cls)
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

        // ─── Get by Rmtcode ───────────────────────────────────────────
        [HttpGet("get-by-rmtcode")]
        public async Task<IActionResult> GetByRmtcode(int rmtcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByRmtcode(rmtcode, tenant));
        }

        // ─── Get by Branch ────────────────────────────────────────────
        [HttpGet("get-by-branch")]
        public async Task<IActionResult> GetByBranch(int branchcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByBranch(branchcode, tenant));
        }

        // ─── Get Next Rmtcode ─────────────────────────────────────────
        [HttpGet("get-next-rmtcode")]
        public async Task<IActionResult> GetNextRmtcode()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetNextRmtcode(tenant));
        }

        // ─── Insert ───────────────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] RoomTypeMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = "Success", rmtcode = data.rmtcode });
        }

        // ─── Update ───────────────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] RoomTypeMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Update(data);
            return Ok(new { message = res });
        }

        // ─── Delete ───────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int rmtcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var res = await _cls.Delete(rmtcode, tenant);
            if (res != "Success")
                return NotFound(new { message = res });

            return Ok(new { message = "Room type deleted successfully" });
        }
    }
}