// Controllers/LabResultEntryController.cs
using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/lab-result-entry")]
    [ApiController]
    public class LabResultEntryController : ControllerBase
    {
        private readonly LabResultEntryClass cls;

        public LabResultEntryController(LabResultEntryClass _cls)
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

        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateStatus(
            [FromBody] LabStatusUpdateRequest req,
            [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.UpdateStatus(req.id, tenant_code, req.status, req.usercode, req.computercode);
            return Ok(result);
        }
    }
}