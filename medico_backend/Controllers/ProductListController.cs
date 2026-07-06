using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductListController : ControllerBase
    {
        private readonly ProductListClass cls;

        public ProductListController(ProductListClass _cls)
        {
            cls = _cls;
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var data = await cls.Get();
            return Ok(data);
        }
        // ─────────────────────────────────────────
        // GET BY TENANT CODE
        // ─────────────────────────────────────────
        [HttpGet("get-by-tenant")]
        public async Task<IActionResult> GetByTenant(string tenant_code)
        {
            if (string.IsNullOrWhiteSpace(tenant_code))
            {
                return BadRequest("tenant_code is required");
            }

            var data = await cls.GetByTenant(tenant_code);

            if (data == null || data.Count == 0)
            {
                return NotFound("No products found for this tenant");
            }

            return Ok(data);
        }
    }
}