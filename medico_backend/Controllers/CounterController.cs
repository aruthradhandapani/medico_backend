using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CounterMasterController : ControllerBase
    {
        private readonly CounterMasterClass cls;

        public CounterMasterController(CounterMasterClass _cls)
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

        [HttpGet("get-by-cntcode")]
        public async Task<IActionResult> GetByCounterCode(decimal cntcode)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetByCounterCode(cntcode, tenant_code);
            if (data == null)
                return NotFound("Data Not Found");

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

        [HttpGet("get-next-cntcode")]
        public async Task<IActionResult> GetNextCounterCode()
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetNextCounterCode(tenant_code);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] CounterMasterModel data)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Insert(data, tenant_code);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CounterMasterModel data)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Update(data, tenant_code);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(decimal cntcode)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Delete(cntcode, tenant_code);
            return Ok(result);
        }
    }
}