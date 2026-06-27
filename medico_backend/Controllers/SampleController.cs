using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SampleMasterController : ControllerBase
    {
        private readonly SampleMasterClass cls;

        public SampleMasterController(SampleMasterClass _cls)
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

        [HttpGet("get-by-scode")]
        public async Task<IActionResult> GetBySCode(decimal scode)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetBySCode(scode, tenant_code);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        [HttpGet("search-by-name")]
        public async Task<IActionResult> SearchByName(string name)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.SearchByName(name, tenant_code);
            return Ok(data);
        }

        [HttpGet("get-next-scode")]
        public async Task<IActionResult> GetNextSCode()
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetNextSCode(tenant_code);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] SampleMasterModel data)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Insert(data, tenant_code);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] SampleMasterModel data)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Update(data, tenant_code);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(decimal scode)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Delete(scode, tenant_code);
            return Ok(result);
        }
    }
}