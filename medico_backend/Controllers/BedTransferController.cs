using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BedTransferController : ControllerBase
    {
        private readonly BedTransferClass _cls;

        public BedTransferController(BedTransferClass cls)
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

        // ─── Get by Custid ────────────────────────────────────────────
        [HttpGet("get-by-custid")]
        public async Task<IActionResult> GetByCustId(decimal custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByCustId(custid, tenant));
        }

        // ─── Get by Visit ─────────────────────────────────────────────
        [HttpGet("get-by-visit")]
        public async Task<IActionResult> GetByVisit(string lastvisitid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetByVisit(lastvisitid, tenant));
        }

        // ─── Get Latest Transfer by Visit (current bed) ──────────────
        [HttpGet("get-latest-by-visit")]
        public async Task<IActionResult> GetLatestByVisit(string lastvisitid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await _cls.GetLatestByVisit(lastvisitid, tenant);
            if (res == null) return NotFound(new { message = "No transfer record found" });
            return Ok(res);
        }

        // ─── Get Active Admissions (not checked out) ─────────────────
        [HttpGet("get-active-admissions")]
        public async Task<IActionResult> GetActiveAdmissions()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _cls.GetActiveAdmissions(tenant));
        }

        // ─── Insert (log transfer / admission / checkout) ────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] BedTransferModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;

            var res = await _cls.Insert(data);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = "Success", transferid = data.transferid });
        }
    }
}