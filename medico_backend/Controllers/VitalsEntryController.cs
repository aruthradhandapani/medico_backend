// ─────────────────────────────────────────
// CONTROLLER — tenant_code read from header only
// ─────────────────────────────────────────
using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VitalsController : ControllerBase
    {
        private readonly VitalsClass cls;

        public VitalsController(VitalsClass _cls)
        {
            cls = _cls;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get([FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.Get(tenant_code);
            return Ok(data);
        }

        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetById(int vitalentryid, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetById(vitalentryid, tenant_code);
            if (data == null)
                return NotFound("Data Not Found");

            return Ok(data);
        }

        [HttpGet("get-by-status")]
        public async Task<IActionResult> GetByStatus(string status, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetByStatus(status, tenant_code);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] VitalsModel data, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            if (string.IsNullOrEmpty(data.custcode))
                return BadRequest("custcode is required in body");

            if (data.dcode == null)
                return BadRequest("dcode is required in body");

            data.tenant_code = tenant_code; // always from header, ignore any value in body

            var result = await cls.Insert(data);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] VitalsModel data, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            data.tenant_code = tenant_code;

            var result = await cls.Update(data);
            return Ok(result);
        }

        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateVitalsStatusRequest req, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.UpdateStatus(req.vitalentryid, tenant_code, req.status, req.usercode, req.computercode);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int vitalentryid, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Delete(vitalentryid, tenant_code);
            return Ok(result);
        }
        [HttpGet("get-all-dummy-list")]
        public async Task<IActionResult> GetAllDummyList([FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetAllDummyList(tenant_code);
            return Ok(data);
        }
        [HttpPost("update-slot-status")]
        public async Task<IActionResult> UpdateSlotStatus([FromBody] UpdateSlotStatusRequest req, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.UpdateSlotStatus(req.vitalentryid, tenant_code, req.slot, req.status, req.usercode, req.computercode);
            return Ok(result);
        }
    }

    public class UpdateVitalsStatusRequest
    {
        public int vitalentryid { get; set; }
        public string status { get; set; } = "";
        public int usercode { get; set; } = 1;
        public int computercode { get; set; } = 1;
    }
    public class UpdateSlotStatusRequest
    {
        public int vitalentryid { get; set; }
        public string slot { get; set; } = "";   // "in1", "in2", "in3", "in4", or "in5"
        public string status { get; set; } = "";
        public int usercode { get; set; } = 1;
        public int computercode { get; set; } = 1;
    }
}