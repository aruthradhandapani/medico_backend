using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PrefixMasterController : ControllerBase
    {
        private readonly PrefixMasterClass cls;

        public PrefixMasterController(PrefixMasterClass _cls)
        {
            cls = _cls;
        }

        // ─────────────────────────────────────────
        // GET ALL PREFIX
        // ─────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var data = await cls.Get();

            return Ok(data);
        }
    }
}