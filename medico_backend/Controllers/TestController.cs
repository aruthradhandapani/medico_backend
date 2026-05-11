using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestMasterController : ControllerBase
    {
        private readonly TestMasterClass cls;

        public TestMasterController(TestMasterClass _cls)
        {
            cls = _cls;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.Get(tenant);

            return Ok(data);
        }

        [HttpGet("get-by-tcode")]
        public async Task<IActionResult> GetByTcode(decimal tcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.GetByTcode(tcode, tenant);

            if (data == null)
            {
                return NotFound("Data Not Found");
            }

            return Ok(data);
        }

        [HttpGet("get-next-tcode")]
        public async Task<IActionResult> GetNextTcode()
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.GetNextTcode(tenant);

            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert(
            [FromBody] TestMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            data.tenant_code = tenant;

            var result = await cls.Insert(data);

            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update(
            [FromBody] TestMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            data.tenant_code = tenant;

            var result = await cls.Update(data);

            return Ok(result);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(decimal tcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var result = await cls.Delete(tcode, tenant);

            return Ok(result);
        }
    }
}