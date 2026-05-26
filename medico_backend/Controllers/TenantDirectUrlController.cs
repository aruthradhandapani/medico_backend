using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TenantDirectUrlController : ControllerBase
    {
        private readonly TenantDirectUrlClass cls;

        public TenantDirectUrlController(TenantDirectUrlClass _cls)
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
        // GET BY ID
        // ─────────────────────────────────────────
        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetById(long id)
        {
            var data = await cls.GetById(id);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] TenantDirectUrlModel data)
        {
            var result = await cls.Insert(data);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] TenantDirectUrlModel data)
        {
            var result = await cls.Update(data);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // SOFT DELETE
        // ─────────────────────────────────────────
        [HttpGet("softdelete")]
        public async Task<IActionResult> SoftDelete(long id)
        {
            var result = await cls.SoftDelete(id);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // HARD DELETE
        // ─────────────────────────────────────────
        [HttpGet("harddelete")]
        public async Task<IActionResult> HardDelete(long id)
        {
            var result = await cls.HardDelete(id);
            return Ok(result);
        }

        // ─────────────────────────────────────────
        // SEARCH BY TENANT NAME
        // ─────────────────────────────────────────
        [HttpGet("search")]
        public async Task<IActionResult> Search(string? tenant_name, string? url)
        {
            var data = await cls.Search(tenant_name, url);
            return Ok(data);
        }
    }
}