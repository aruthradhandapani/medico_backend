using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TenantProductSubscriptionController : ControllerBase
    {
        private readonly TenantProductSubscriptionClass cls;

        public TenantProductSubscriptionController(TenantProductSubscriptionClass _cls)
        {
            cls = _cls;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get(string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code is required");

            var data = await cls.Get(tenant_code);
            return Ok(data);
        }

        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetById(int id, string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code is required");

            var data = await cls.GetById(id, tenant_code);
            if (data == null)
                return NotFound("Data Not Found");

            return Ok(data);
        }

        [HttpGet("get-by-product")]
        public async Task<IActionResult> GetByProduct(string product_id, string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code is required");

            var data = await cls.GetByProduct(product_id, tenant_code);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] TenantProductSubscriptionModel data)
        {
            if (string.IsNullOrEmpty(data.tenant_code))
                return BadRequest("tenant_code is required in body");

            if (string.IsNullOrEmpty(data.product_id))
                return BadRequest("product_id is required in body");

            var result = await cls.Insert(data);
            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] TenantProductSubscriptionModel data)
        {
            if (string.IsNullOrEmpty(data.tenant_code))
                return BadRequest("tenant_code is required in body");

            var result = await cls.Update(data);
            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int id, string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("tenant_code is required");

            var result = await cls.Delete(id, tenant_code);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // GET ALL ACROSS TENANTS (admin panel — no tenant_code filter)
        // ─────────────────────────────────────────
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllAcrossTenants()
        {
            var data = await cls.GetAllAcrossTenants();
            return Ok(data);
        }
    }
}