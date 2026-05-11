using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestFeeMasterController : ControllerBase
    {
        private readonly TestFeeMasterClass cls;

        public TestFeeMasterController(TestFeeMasterClass _cls)
        {
            cls = _cls;
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.Get(tenant);

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // GET BY TFCODE
        // ─────────────────────────────────────────
        [HttpGet("get-by-tfcode")]
        public async Task<IActionResult> GetByTfCode(decimal tfcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.GetByTfCode(tfcode, tenant);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // GET NEXT TFCODE
        // ─────────────────────────────────────────
        [HttpGet("get-next-tfcode")]
        public async Task<IActionResult> GetNextTfCode()
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.GetNextTfCode(tenant);

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert(
            [FromBody] TestFeeMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            data.tenant_code = tenant;

            var result = await cls.Insert(data);

            return Ok(result);
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        [HttpPost("update")]
        public async Task<IActionResult> Update(
            [FromBody] TestFeeMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            data.tenant_code = tenant;

            var result = await cls.Update(data);

            return Ok(result);
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(decimal tfcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var result = await cls.Delete(tfcode, tenant);

            return Ok(result);
        }
    }
}