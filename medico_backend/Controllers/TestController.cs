
using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestMasterController : ControllerBase
    {
        private readonly TestMasterClass cls;

        public TestMasterController(TestMasterClass _cls)
        {
            cls = _cls;
        }

        private string? TenantCode =>
            Request.Headers.TryGetValue("tenant_code", out var v) && !string.IsNullOrWhiteSpace(v)
                ? v.ToString() : null;


        // ── GET ALL ───────────────────────────────────────────────────────────

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            if (TenantCode is null)
                return BadRequest(new { message = "tenant_code is required in header." });

            var data = await cls.Get(TenantCode);
            return Ok(data);
        }

        // ── GET BY TCODE ──────────────────────────────────────────────────────

        [HttpGet("get-by-tcode")]
        public async Task<IActionResult> GetByTcode([FromQuery] long tcode)
        {
            if (tcode <= 0) return BadRequest(new { message = "Invalid tcode." });
            if (TenantCode is null)
                return BadRequest(new { message = "tenant_code is required in header." });

            var data = await cls.GetByTcode(tcode, TenantCode);
            return data is null ? NotFound(new { message = "Data Not Found" }) : Ok(data);
        }

        // ── INSERT ────────────────────────────────────────────────────────────

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] TestMasterModel data)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (TenantCode is null)
                return BadRequest(new { message = "tenant_code is required in header." });


            var (result, tcode) = await cls.Insert(data, TenantCode);

            return result == "Success"
                ? Ok(new { message = "Inserted Successfully", tcode })
                : BadRequest(new { message = result });
        }

        // ── UPDATE ────────────────────────────────────────────────────────────

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] TestMasterModel data)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (data.tcode <= 0) return BadRequest(new { message = "Invalid tcode." });
            if (TenantCode is null)
                return BadRequest(new { message = "tenant_code is required in header." });


            var result = await cls.Update(data, TenantCode);

            return result == "Success"
                ? Ok(new { message = "Updated Successfully" })
                : BadRequest(new { message = result });
        }

        // ── SOFT DELETE ───────────────────────────────────────────────────────

        [HttpGet("delete")]
        public async Task<IActionResult> SoftDelete([FromQuery] long tcode)
        {
            if (tcode <= 0) return BadRequest(new { message = "Invalid tcode." });
            if (TenantCode is null)
                return BadRequest(new { message = "tenant_code is required in header." });

            var result = await cls.SoftDelete(tcode, TenantCode);

            return result == "Success"
                ? Ok(new { message = "Soft Deleted Successfully" })
                : BadRequest(new { message = result });
        }
    }
}