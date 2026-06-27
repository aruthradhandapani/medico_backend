using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CountryMasterController : ControllerBase
    {
        private readonly CountryMasterClass cls;

        public CountryMasterController(CountryMasterClass _cls)
        {
            cls = _cls;
        }

        private string? GetTenantCode()
        {
            return Request.Headers["tenant_code"].FirstOrDefault();
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.Get(tenant_code);
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // GET BY COUNTRYCODE
        // ─────────────────────────────────────────
        [HttpGet("get-by-countrycode")]
        public async Task<IActionResult> GetByCountryCode(int countrycode)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetByCountryCode(countrycode, tenant_code);
            if (data == null)
                return NotFound("Data Not Found");

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // SEARCH BY COUNTRY NAME
        // ─────────────────────────────────────────
        [HttpGet("search-by-countryname")]
        public async Task<IActionResult> SearchByCountryName(string countryname)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.SearchByCountryName(countryname, tenant_code);
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // GET NEXT COUNTRYCODE
        // ─────────────────────────────────────────
        [HttpGet("get-next-countrycode")]
        public async Task<IActionResult> GetNextCountryCode()
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await cls.GetNextCountryCode(tenant_code);
            return Ok(data);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] CountryMasterModel data)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Insert(data, tenant_code);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CountryMasterModel data)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Update(data, tenant_code);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int countrycode)
        {
            var tenant_code = GetTenantCode();
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var result = await cls.Delete(countrycode, tenant_code);
            return Ok(result);
        }
    }
}