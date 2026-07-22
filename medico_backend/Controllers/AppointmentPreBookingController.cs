using medico_backend.Class;
using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentPreBookingController : ControllerBase
    {
        private readonly AppointmentPreBookingClass cls;

        public AppointmentPreBookingController(AppointmentPreBookingClass _cls)
        {
            cls = _cls;
        }

        [HttpPost("add")]
        public async Task<IActionResult> Add([FromBody] AddAppointmentPreBookingRequest req, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Add(tenant_code, req);
            return Ok(result);
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
        public async Task<IActionResult> GetById(long preferenceid, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetById(preferenceid, tenant_code);
            if (data == null)
                return NotFound("Data Not Found");

            return Ok(data);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] UpdateAppointmentPreBookingRequest req, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Update(tenant_code, req);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(long preferenceid, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Delete(preferenceid, tenant_code);
            return Ok(result);
        }
        [HttpPost("mark-visited")]
        public async Task<IActionResult> MarkVisited([FromBody] MarkVisitedRequest req, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.MarkVisited(req.preferenceid, tenant_code, req.in1, req.in2, req.in3, req.in4, req.in5, req.test_name, req.arrival_time, req.is_vip, req.usercode, req.computercode);
            return Ok(result);
        }
    }
}