using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeeTypeMasterController : ControllerBase
    {
        private readonly FeeTypeMasterClass cls;

        public FeeTypeMasterController(FeeTypeMasterClass _cls)
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
        // GET BY FTCODE
        // ─────────────────────────────────────────
        [HttpGet("get-by-ftcode")]
        public async Task<IActionResult> GetByFtCode(int ftcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.GetByFtCode(ftcode, tenant);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        [HttpGet("search-by-name")]
        public async Task<IActionResult> SearchByName(string name)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.SearchByName(name, tenant);

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // GET NEXT FTCODE
        // ─────────────────────────────────────────
        [HttpGet("get-next-ftcode")]
        public async Task<IActionResult> GetNextFtCode()
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.GetNextFtCode(tenant);

            return Ok(data);
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        [HttpPost("insert")]
        public async Task<IActionResult> Insert(
            [FromBody] FeeTypeMasterModel data)
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
            [FromBody] FeeTypeMasterModel data)
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
        public async Task<IActionResult> Delete(int ftcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var result = await cls.Delete(ftcode, tenant);

            return Ok(result);
        }
    }
}