using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly TenantClass _cls;

        public TenantController(TenantClass cls)
        {
            _cls = cls;
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _cls.GetAll());
        }

        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetByTenantId(Guid tenant_id)
        {
            var res = await _cls.GetByTenantId(tenant_id);
            if (res == null) return NotFound(new { message = "Tenant not found" });
            return Ok(res);
        }

        [HttpGet("get-by-code")]
        public async Task<IActionResult> GetByTenantCode(string tenant_code)
        {
            var res = await _cls.GetByTenantCode(tenant_code);
            if (res == null) return NotFound(new { message = "Tenant not found" });
            return Ok(res);
        }


        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] UpdateTenantRequest req)
        {
            var res = await _cls.Update(req);
            if (res != "Success")
                return BadRequest(new { message = res });

            return Ok(new { message = res });
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(Guid tenant_id)
        {
            var res = await _cls.Delete(tenant_id);
            if (res != "Success")
                return NotFound(new { message = res });

            return Ok(new { message = "Tenant deleted successfully" });
        }
    }
}