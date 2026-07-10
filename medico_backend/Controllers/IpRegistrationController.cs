using medico_backend.Class;
using Microsoft.AspNetCore.Mvc;
using static medico_backend.Model.IPRegistrationModel;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IpRegistrationController : ControllerBase
    {
        private readonly IpRegistrationClass cls;

        public IpRegistrationController(IpRegistrationClass _cls)
        {
            cls = _cls;
        }

        [HttpPost("admit")]
        public async Task<IActionResult> Admit([FromBody] CreateIpRegistrationRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.CreateIpRegistration(req, tenant);
            return Ok(res);
        }

        [HttpPost("discharge")]
        public async Task<IActionResult> Discharge([FromBody] DischargeRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.Discharge(req, tenant);
            return Ok(res);
        }

        // NOTE: bed/room transfers now go through BedTransferController -> POST /api/BedTransfer/insert
        // That endpoint updates ip_registration's bed/room fields AND bed_status in one transaction.

        [HttpGet("get")]
        public async Task<IActionResult> GetAll(string? ip_status, int? dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.GetAll(tenant, ip_status, dcode);
            return Ok(res);
        }

        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetById(Guid ip_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.GetById(ip_id, tenant);
            if (res == null) return NotFound(new { message = "IP Registration not found" });
            return Ok(res);
        }

        [HttpGet("active-admissions")]
        public async Task<IActionResult> GetActiveAdmissions()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.GetActiveAdmissions(tenant);
            return Ok(res);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] UpdateIpRegistrationRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.Update(req, tenant);
            return Ok(res);
        }

        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel([FromBody] CancelAdmissionRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.CancelAdmission(req, tenant);
            return Ok(res);
        }
    }
}