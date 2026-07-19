// Controllers/OgScreenController.cs
using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/og-screen")]
    [ApiController]
    public class OgScreenController : ControllerBase
    {
        private readonly OgScreenClass cls;

        public OgScreenController(OgScreenClass _cls)
        {
            cls = _cls;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromHeader(Name = "tenant_code")] string tenant_code,
            string? name, DateTime? date)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.Search(tenant_code, name, date);
            return Ok(data);
        }

        [HttpPost("mark-ongoing")]
        public async Task<IActionResult> MarkOngoing(
            [FromBody] OgScreenStatusUpdateRequest req,
            [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.MarkOngoing(req.id, tenant_code, req.usercode, req.computercode);
            return Ok(result);
        }

        [HttpPost("mark-completed")]
        public async Task<IActionResult> MarkCompleted(
            [FromBody] OgScreenStatusUpdateRequest req,
            [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.MarkCompleted(req.id, tenant_code, req.notes, req.usercode, req.computercode);
            return Ok(result);
        }
    }
}