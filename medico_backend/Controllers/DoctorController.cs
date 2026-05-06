using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorMasterController : ControllerBase
    {
        private readonly DoctorMasterClass cls;

        public DoctorMasterController(DoctorMasterClass _cls)
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

        [HttpGet("get-by-dcode")]
        public async Task<IActionResult> GetByDcode(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetByDcode(dcode, tenant);
            return Ok(data);
        }

        [HttpGet("get-consultants")]
        public async Task<IActionResult> GetConsultants()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetConsultants(tenant);
            return Ok(data);
        }

        [HttpGet("get-referrals")]
        public async Task<IActionResult> GetReferrals()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetReferrals(tenant);
            return Ok(data);
        }

        [HttpGet("get-next-dcode")]
        public async Task<IActionResult> GetNextDcode()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetNextDcode(tenant);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] DoctorMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.Insert(data);
            return Ok(res);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] DoctorMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.Update(data);
            return Ok(res);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.Delete(dcode, tenant);
            return Ok(res);
        }
    }
}