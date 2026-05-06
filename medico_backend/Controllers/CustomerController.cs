using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerMasterController : ControllerBase
    {
        private readonly CustomerMasterClass cls;

        public CustomerMasterController(CustomerMasterClass _cls)
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

        [HttpGet("get-by-custid")]
        public async Task<IActionResult> GetByCustId(decimal custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetByCustId(custid, tenant);
            return Ok(data);
        }

        [HttpGet("get-by-mobile")]
        public async Task<IActionResult> GetByMobile(string mobile)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetByMobile(mobile, tenant);
            return Ok(data);
        }

        [HttpGet("search-by-name")]
        public async Task<IActionResult> SearchByName(string name)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.SearchByName(name, tenant);
            return Ok(data);
        }

        [HttpGet("get-insurance-patients")]
        public async Task<IActionResult> GetInsurancePatients()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetInsurancePatients(tenant);
            return Ok(data);
        }

        [HttpGet("get-ip-patients")]
        public async Task<IActionResult> GetIPPatients()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetIPPatients(tenant);
            return Ok(data);
        }

        [HttpGet("get-op-patients")]
        public async Task<IActionResult> GetOPPatients()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetOPPatients(tenant);
            return Ok(data);
        }

        [HttpGet("get-next-custid")]
        public async Task<IActionResult> GetNextCustId()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetNextCustId(tenant);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<IActionResult> Insert([FromBody] CustomerMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.Insert(data);
            return Ok(res);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CustomerMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.Update(data);
            return Ok(res);
        }

        [HttpGet("delete")]
        public async Task<IActionResult> Delete(decimal custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.Delete(custid, tenant);
            return Ok(res);
        }
    }
}