using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OpChargeSlabController : ControllerBase
    {
        private readonly OpChargeSlabClass _cls;

        public OpChargeSlabController(OpChargeSlabClass cls)
        {
            _cls = cls;
        }

    
        // ─── Get all slabs for a doctor ─────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get([FromQuery] int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code is required" });

            var res = await _cls.GetSlabs(tenant, dcode);
            return Ok(res);
        }

        // ─── Soft delete a slab ──────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete([FromQuery] int slabid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code is required" });

            var res = await _cls.DeleteSlab(slabid, tenant);
            return res == "Success" ? Ok(new { message = res }) : BadRequest(new { message = res });
        }
        // ─── Add slabs in bulk (list-wise, one call per doctor) ─────────
        [HttpPost("add-list")]
        public async Task<IActionResult> AddList([FromBody] List<OpChargeSlabModel> models)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code is required" });

            if (models == null || models.Count == 0)
                return BadRequest(new { message = "At least one slab is required" });

            var res = await _cls.AddSlabs(models, tenant);
            return res == "Success" ? Ok(new { message = res }) : BadRequest(new { message = res });
        }
        [HttpPost("update-list")]
        public async Task<IActionResult> UpdateList([FromBody] List<OpChargeSlabModel> models)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrEmpty(tenant))
                return BadRequest(new { message = "Tenant code is required" });

            if (models == null || models.Count == 0)
                return BadRequest(new { message = "At least one slab is required" });

            var res = await _cls.UpdateSlabs(models, tenant);
            return res == "Success" ? Ok(new { message = res }) : BadRequest(new { message = res });
        }
    }
}