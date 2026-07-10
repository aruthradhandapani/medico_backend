using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BedStatusController : ControllerBase
    {
        private readonly BedStatusClass _cls;

        public BedStatusController(BedStatusClass cls)
        {
            _cls = cls;
        }

        // ─── Get All (full log) ────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> GetAll()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetAll(tenant));
        }

        // ─── Get History For a Specific Bed ────────────────────────────
        [HttpGet("get-by-bed")]
        public async Task<IActionResult> GetByBed(int bedcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByBed(bedcode, tenant));
        }

        // ─── Get History For a Patient Admission ───────────────────────
        [HttpGet("get-by-ip")]
        public async Task<IActionResult> GetByIpId(Guid ip_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByIpId(ip_id, tenant));
        }

        // ─── Get Beds Pending Cleaning (vacated, not yet cleaned) ──────
        [HttpGet("pending-cleaning")]
        public async Task<IActionResult> GetPendingCleaning()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetPendingCleaning(tenant));
        }

        // ─── Mark Bed Cleaned (housekeeping confirms bed is ready) ─────
        [HttpPost("mark-cleaned")]
        public async Task<IActionResult> MarkCleaned([FromBody] MarkBedCleanedRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await _cls.MarkCleaned(req, tenant);

            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = res });
        }
    }
}