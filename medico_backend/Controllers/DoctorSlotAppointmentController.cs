using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorAppointmentSlotController : ControllerBase
    {
        private readonly DoctorAppointmentSlotClass cls;

        public DoctorAppointmentSlotController(DoctorAppointmentSlotClass _cls)
        {
            cls = _cls;
        }

        // ═══════════════════════════════════════════
        // MASTER
        // ═══════════════════════════════════════════

        [HttpGet("master/get")]
        public async Task<IActionResult> GetAllMaster()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.GetAllMaster(tenant));
        }

        [HttpGet("master/get-by-doctor")]
        public async Task<IActionResult> GetMasterByDoctor(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.GetMasterByDoctor(dcode, tenant));
        }

        [HttpPost("master/insert")]
        public async Task<IActionResult> InsertMaster(
            [FromBody] DoctorAppointmentSlotMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            return Ok(await cls.InsertMaster(data));
        }

        [HttpPost("master/update")]
        public async Task<IActionResult> UpdateMaster(
            [FromBody] DoctorAppointmentSlotMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            return Ok(await cls.UpdateMaster(data));
        }

        // ✅ Fixed — DELETE not GET
        [HttpGet("master/delete")]
        public async Task<IActionResult> DeleteMaster(Guid slot_master_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.DeleteMaster(slot_master_id, tenant));
        }

        // ═══════════════════════════════════════════
        // DETAILS
        // ═══════════════════════════════════════════

        [HttpGet("details/get")]
        public async Task<IActionResult> GetAllDetails()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.GetAllDetails(tenant));
        }

        [HttpGet("details/get-by-date")]
        public async Task<IActionResult> GetDetailsByDate(int dcode, DateOnly appointment_date)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.GetDetailsByDate(dcode, appointment_date, tenant));
        }

        [HttpPost("details/insert")]
        public async Task<IActionResult> InsertDetails(
            [FromBody] DoctorAppointmentSlotDetailsModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            return Ok(await cls.InsertDetails(data));
        }

        [HttpPost("details/update")]
        public async Task<IActionResult> UpdateDetails(
            [FromBody] DoctorAppointmentSlotDetailsModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            return Ok(await cls.UpdateDetails(data));
        }

        // ✅ Fixed — DELETE not GET
        [HttpDelete("details/delete")]
        public async Task<IActionResult> DeleteDetails(Guid slot_detail_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.DeleteDetails(slot_detail_id, tenant));
        }

        // ✅ Fixed — POST with body, bulk insert
        [HttpPost("details/bulk-insert")]
        public async Task<IActionResult> BulkInsertDetails(
            [FromBody] BulkInsertSlotDetailsRequest request)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.BulkInsertDetails(request, tenant));
        }
    }
}