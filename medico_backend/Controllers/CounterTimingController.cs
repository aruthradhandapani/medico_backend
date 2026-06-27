using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CounterTimingController : ControllerBase
    {
        private readonly CounterTimingClass cls;

        public CounterTimingController(CounterTimingClass _cls)
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

        [HttpGet("get-by-cnttid")]
        public async Task<IActionResult> GetByCnttid(string cnttid)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetByCnttid(cnttid, tenant_code);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        [HttpGet("get-by-cntcode")]
        public async Task<IActionResult> GetByCntcode(int cntcode)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetByCntcode(cntcode, tenant_code);
            return Ok(data);
        }

        [HttpGet("get-open-shift")]
        public async Task<IActionResult> GetOpenShift(int bhcode, int cntcode)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetOpenShift(bhcode, cntcode, tenant_code);

            if (data == null)
            {
                return NotFound("No open shift found");
            }

            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] CounterTimingModel data)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Insert(data, tenant_code);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CounterTimingModel data)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Update(data, tenant_code);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(string cnttid)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Delete(cnttid, tenant_code);
            return Ok(result);
        }
    }
}