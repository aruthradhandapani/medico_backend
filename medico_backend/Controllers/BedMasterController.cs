using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BedMasterController : ControllerBase
    {
        private readonly BedMasterClass _cls;

        public BedMasterController(BedMasterClass cls)
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

        // ─── Get by Bedcode ───────────────────────────────────────────
        [HttpGet("get-by-bedcode")]
        public async Task<IActionResult> GetByBedcode(int bedcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByBedcode(bedcode, tenant));
        }

        // ─── Get by Branch ────────────────────────────────────────────
        [HttpGet("get-by-branch")]
        public async Task<IActionResult> GetByBranch(int branchcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByBranch(branchcode, tenant));
        }

        // ─── Get by Ward ──────────────────────────────────────────────
        [HttpGet("get-by-ward")]
        public async Task<IActionResult> GetByWard(int wrdcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByWard(wrdcode, tenant));
        }

        // ─── Get by Room Type ─────────────────────────────────────────
        [HttpGet("get-by-roomtype")]
        public async Task<IActionResult> GetByRoomType(int rmtcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByRoomType(rmtcode, tenant));
        }

        // ─── Insert ───────────────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] BedMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = "Success", bedcode = data.bedcode });
        }

        // ─── Update ───────────────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] BedMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Update(data);
            return Ok(new { message = res });
        }

        // ─── Delete ───────────────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int bedcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var res = await _cls.Delete(bedcode, tenant);
            if (res != "Success")
                return NotFound(new { message = res });

            return Ok(new { message = "Bed deleted successfully" });
        }
        // ─── Get Available Beds (with optional block/floor/ward/roomtype filters) ─
        [HttpGet("get-available")]
        public async Task<IActionResult> GetAvailableBeds(
            int? branchcode, int? blockcode, int? flrcode, int? wrdcode, int? rmtcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await _cls.GetAvailableBeds(tenant, branchcode, blockcode, flrcode, wrdcode, rmtcode);
            return Ok(res);
        }
        // ─── Get Occupied Beds (companion view) ────────────────────────
        [HttpGet("get-occupied")]
        public async Task<IActionResult> GetOccupiedBeds(
            int? branchcode, int? blockcode, int? flrcode, int? wrdcode, int? rmtcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await _cls.GetOccupiedBeds(tenant, branchcode, blockcode, flrcode, wrdcode, rmtcode);
            return Ok(res);
        }

    }
}