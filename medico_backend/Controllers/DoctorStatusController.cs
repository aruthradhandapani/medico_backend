using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorCurrentStatusController : ControllerBase
    {
        private readonly DoctorCurrentStatusClass cls;

        public DoctorCurrentStatusController(DoctorCurrentStatusClass _cls)
        {
            cls = _cls;
        }

        private string? GetTenantCode()
        {
            return Request.Headers["tenant_code"].FirstOrDefault();
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.Get(tenant_code);
            return Ok(data);
        }

        [HttpGet("get-by-dcode")]
        public async Task<IActionResult> GetByDcode(long dcode)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetByDcode(dcode, tenant_code);
            if (data == null)
                return NotFound("Data Not Found");

            return Ok(data);
        }

        [HttpGet("search-by-status")]
        public async Task<IActionResult> SearchByStatus(string status)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.SearchByStatus(status, tenant_code);
            return Ok(data);
        }

        [HttpPost("set-status")]
        public async Task<IActionResult> SetStatus([FromBody] DoctorCurrentStatusModel data)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.SetStatus(data, tenant_code);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] DoctorCurrentStatusModel data)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Update(data, tenant_code);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(long status_id)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Delete(status_id, tenant_code);
            return Ok(result);
        }
    }
}