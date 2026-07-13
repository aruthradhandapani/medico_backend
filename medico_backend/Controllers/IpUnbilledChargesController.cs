using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Asn1.Ocsp;

namespace medico_backend.Controllers
{
    public class IpUnbilledChargesController
    {
        [Route("api/[controller]")]
        [ApiController]
        public class IpUnbilledChargeController : ControllerBase
        {
            private readonly IpUnbilledChargeClass _cls;
            public IpUnbilledChargeController(IpUnbilledChargeClass cls) { _cls = cls; }

            [HttpGet("get-by-ip")]
            public async Task<IActionResult> GetByIp(Guid ip_id)
            {
                var tenant = Request.Headers["tenant_code"].ToString();
                return Ok(await _cls.GetByIp(ip_id, tenant));
            }

            [HttpGet("get-unbilled")]
            public async Task<IActionResult> GetUnbilled(Guid? ip_id, string? entrytype)
            {
                var tenant = Request.Headers["tenant_code"].ToString();
                return Ok(await _cls.GetUnbilled(tenant, ip_id, entrytype));
            }

            [HttpPost("add-nursing")]
            public async Task<IActionResult> AddNursing([FromBody] AddIpNursingChargeRequest req)
            {
                var tenant = Request.Headers["tenant_code"].ToString();
                return Ok(new { unbilledid = await _cls.AddNursingCharge(req, tenant) });
            }

            [HttpPost("add-test")]
            public async Task<IActionResult> AddTest([FromBody] AddIpTestChargeRequest req)
            {
                var tenant = Request.Headers["tenant_code"].ToString();
                return Ok(new { unbilledid = await _cls.AddTestCharge(req, tenant) });
            }

            [HttpPost("mark-billed")]
            public async Task<IActionResult> MarkBilled([FromBody] MarkIpChargesBilledRequest req)
            {
                var tenant = Request.Headers["tenant_code"].ToString();
                var res = await _cls.MarkBilled(req, tenant);
                if (res != "Success") return BadRequest(new { message = res });
                return Ok(new { message = res });
            }
        }
    }
}
