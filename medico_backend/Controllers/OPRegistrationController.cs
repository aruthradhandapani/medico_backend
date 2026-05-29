using medico_backend.Class;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Asn1.Ocsp;
using static medico_backend.Model.OPRegistrationModel;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OpRegistrationController : ControllerBase
    {
        private readonly OpRegistrationClass cls;

        public OpRegistrationController(OpRegistrationClass _cls)
        {
            cls = _cls;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] OpRegistrationModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.CreateOpRegistration(data);
            return Ok(res);
        }

        [HttpPost("update-visit-status")]
        public async Task<IActionResult> UpdateVisitStatus(
            Guid op_id, string visit_status)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.UpdateVisitStatus(op_id, visit_status, tenant);
            return Ok(res);
        }

        [HttpPost("save-vitals")]
        public async Task<IActionResult> SaveVitals([FromBody] PatientVitalsModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.SaveVitals(data);
            return Ok(res);
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetTodayOpList(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetTodayOpList(dcode, tenant);
            return Ok(data);
        }

        // GET api/OpRegistration/all
        // Query params: dcode, from_date (yyyy-MM-dd), to_date, visit_status
        [HttpGet("all")]
        public async Task<IActionResult> GetAllOpList(
            int? dcode, DateOnly? from_date, DateOnly? to_date, string? visit_status)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetAllOpList(tenant, dcode, from_date, to_date, visit_status);
            return Ok(data);
        }

        // PUT api/OpRegistration/update-vitals
        [HttpPost("update-vitals")]
        public async Task<IActionResult> UpdateVitals([FromBody] PatientVitalsModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.UpdateVitals(data);
            return Ok(res);
        }

        // GET api/OpRegistration/vitals/all
        // Query params: op_id OR custid (at least one required)
        [HttpGet("vitals/all")]
        public async Task<IActionResult> GetAllVitals(Guid? op_id, decimal? custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            if (op_id == null && custid == null)
                return BadRequest("Provide at least one filter: op_id or custid");

            var data = await cls.GetAllVitals(tenant, op_id, custid);
            return Ok(data);
        }

        // GET api/OpRegistration/vitals/detail?vital_id=...
        [HttpGet("vitals/detail")]
        public async Task<IActionResult> GetVitalById(Guid vital_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetVitalById(vital_id, tenant);

            if (data == null) return NotFound("Vital record not found");
            return Ok(data);
        }
    }
}
