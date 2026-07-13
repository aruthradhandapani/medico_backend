using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestGroupRateController : ControllerBase
    {
        private readonly TestGroupRateClass _cls;

        public TestGroupRateController(TestGroupRateClass cls)
        {
            _cls = cls;
        }

        // ─── Get All (admin/reference) ─────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetAll(tenant));
        }

        // ─── Get Split-up Charges for a Room Type ──────────────────
        [HttpGet("get-by-rmtcode")]
        public async Task<IActionResult> GetByRmtcode(int rmtcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByRmtcode(rmtcode, tenant));
        }

        // ─── Insert Single Rate Row ─────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] TestGroupRateModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            if (res != "Success") return BadRequest(new { message = res });
            return Ok(new { message = "Success", id = data.id });
        }

        // ─── Update Single Rate Row ─────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] TestGroupRateModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Update(data);
            return Ok(new { message = res });
        }

        // ─── Delete Single Rate Row ──────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var res = await _cls.Delete(id, tenant);
            if (res != "Success") return NotFound(new { message = res });
            return Ok(new { message = "Deleted successfully" });
        }

        // ─── Save Whole Split-up List for a Room Type (replace-all) ──
        [HttpPost("save-for-roomtype")]
        public async Task<IActionResult> SaveForRoomType([FromBody] SaveRoomTypeRatesRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await _cls.SaveForRoomType(req, tenant);

            if (res != "Success") return BadRequest(new { message = res });
            return Ok(new { message = res });
        }
    }
}