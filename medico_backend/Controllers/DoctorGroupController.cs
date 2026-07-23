using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorGroupMasterController : ControllerBase
    {
        private readonly DoctorGroupMasterClass _cls;

        public DoctorGroupMasterController(DoctorGroupMasterClass cls)
        {
            _cls = cls;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get([FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            return Ok(await _cls.Get(tenant_code));
        }

        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetById(long group_id, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            var data = await _cls.GetById(group_id, tenant_code);
            if (data == null) return NotFound("Data Not Found");
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] DoctorGroupMasterModel data, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            data.tenant_code = tenant_code;
            return Ok(new { message = await _cls.Insert(data) });
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] DoctorGroupMasterModel data, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            data.tenant_code = tenant_code;
            return Ok(new { message = await _cls.Update(data) });
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(long group_id, [FromHeader(Name = "tenant_code")] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code header is required");

            return Ok(new { message = await _cls.Delete(group_id, tenant_code) });
        }
    }
}