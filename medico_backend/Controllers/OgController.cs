using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OgQueueController : ControllerBase
    {
        private readonly OgQueueClass cls;

        public OgQueueController(OgQueueClass _cls)
        {
            cls = _cls;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get(
            [FromHeader(Name = "tenant_code")] string tenant_code,
            string? name,
            DateTime? date)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.Get(tenant_code, name, date);
            return Ok(data);
        }

        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetById(int ogentryid, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetById(ogentryid, tenant_code);
            if (data == null)
                return NotFound("Data Not Found");

            return Ok(data);
        }

        [HttpGet("get-by-doctor")]
        public async Task<IActionResult> GetByDoctor(int dcode, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetByDoctor(dcode, tenant_code);
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

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int ogentryid, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Delete(ogentryid, tenant_code);
            return Ok(result);
        }
        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateOgStatusRequest req, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.UpdateStatus(req.ogentryid, tenant_code, req.status, req.usercode, req.computercode);
            if (result == null)
                return NotFound("Record not found");

            return Ok(result);
        }

        [HttpPost("update-out-time")]
        public async Task<IActionResult> UpdateOutTime([FromBody] UpdateOgOutTimeRequest req, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.UpdateOutTime(req.ogentryid, tenant_code, req.out_time, req.status, req.notes, req.usercode, req.computercode);
            if (result == null)
                return NotFound("Record not found");

            return Ok(result);
        }

        [HttpGet("test-completed-list")]
        public async Task<IActionResult> GetTestCompletedList(
            [FromHeader(Name = "tenant_code")] string tenant_code,
            string? name, DateTime? date, int? dcode)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetTestCompletedList(tenant_code, name, date, dcode);
            return Ok(data);
        }
        [HttpGet("lab-scan-list")]
        public async Task<IActionResult> GetLabScanList(
    [FromHeader(Name = "tenant_code")] string tenant_code,
    string? name, DateTime? date, string? status)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetLabScanList(tenant_code, name, date, status);
            return Ok(data);
        }

        [HttpGet("consultation-list")]
        public async Task<IActionResult> GetConsultationList(
            [FromHeader(Name = "tenant_code")] string tenant_code,
            string? name, DateTime? date, string? status)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetConsultationList(tenant_code, name, date, status);
            return Ok(data);
        }
        [HttpGet("merged-list")]
        public async Task<IActionResult> GetMergedList(
            [FromHeader(Name = "tenant_code")] string tenant_code,
            string? name, DateTime? date, string? status, string? list_type)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetMergedList(tenant_code, name, date, status, list_type);
            return Ok(data);
        }
        
    }

}