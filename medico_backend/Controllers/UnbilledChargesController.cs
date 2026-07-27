using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UnbilledChargesController : ControllerBase
    {
        private readonly UnbilledChargesClass cls;
        public UnbilledChargesController(UnbilledChargesClass _cls) => cls = _cls;

        // POST api/UnbilledCharges/add-consultation
        [HttpPost("add-consultation")]
        public async Task<IActionResult> AddConsultation([FromBody] AddUnbilledConsultationRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrWhiteSpace(tenant))
                return BadRequest("tenant_code header is required");
            if (string.IsNullOrWhiteSpace(req.op_id))
                return BadRequest("op_id is required");

            var res = await cls.AddConsultationCharge(req, tenant);

            if (!res.StartsWith("Success"))
                return BadRequest(new { message = res });

            return Ok(new { message = res });
        }

        // GET api/UnbilledCharges/by-visit?opvisitid=...   (OP)
        // GET api/UnbilledCharges/by-visit?ip_id=...        (IP)
        [HttpGet("by-visit")]
        public async Task<IActionResult> GetByVisit(
    [FromQuery] string? opvisitid, [FromQuery] string? ip_id)
        {
            if (string.IsNullOrWhiteSpace(opvisitid) && string.IsNullOrWhiteSpace(ip_id))
                return BadRequest("opvisitid or ip_id is required");

            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrWhiteSpace(tenant))
                return BadRequest("tenant_code header is required");

            // Keep ROOMRENT fresh before listing — same pattern as ip-room-rent-summary
            if (!string.IsNullOrWhiteSpace(ip_id) && Guid.TryParse(ip_id, out var ipGuid))
                await cls.RecalculateRoomRent(ipGuid, tenant);

            var data = await cls.GetUnbilledByVisit(opvisitid, ip_id, tenant);
            return Ok(data);
        }

        // GET api/UnbilledCharges/by-customer?custid=...
        [HttpGet("by-customer")]
        public async Task<IActionResult> GetByCustomer([FromQuery] decimal custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrWhiteSpace(tenant))
                return BadRequest("tenant_code header is required");
            if (custid <= 0)
                return BadRequest("custid is required");

            var data = await cls.GetUnbilledByCustomer(custid, tenant);
            return Ok(data);
        }
        [HttpPost("recalculate-room-rent")]
        public async Task<IActionResult> RecalcRoomRent(Guid ip_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrWhiteSpace(tenant))
                return BadRequest("tenant_code header is required");

            var res = await cls.RecalculateRoomRent(ip_id, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("ip-room-rent-summary")]
        public async Task<IActionResult> IpRoomRentSummary(Guid ip_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrWhiteSpace(tenant))
                return BadRequest("tenant_code header is required");

            await cls.RecalculateRoomRent(ip_id, tenant);       // always fresh
            var data = await cls.GetIpRoomRentSummary(ip_id, tenant);
            return Ok(data);
        }

        [HttpGet("room-testgroup-breakdown")]
        public async Task<IActionResult> RoomTestGroupBreakdown(int rmtcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");
            return Ok(await cls.GetTestGroupBreakdown(rmtcode, tenant));
        }
    }
}