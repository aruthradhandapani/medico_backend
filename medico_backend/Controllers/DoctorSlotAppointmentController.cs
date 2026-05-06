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
        // MASTER ENDPOINTS
        // ═══════════════════════════════════════════

        [HttpGet("master/get")]
        public async Task<IActionResult> GetAllMaster()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetAllMaster(tenant);
            return Ok(data);
        }

        [HttpGet("master/get-by-doctor")]
        public async Task<IActionResult> GetMasterByDoctor(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetMasterByDoctor(dcode, tenant);
            return Ok(data);
        }

        
        [HttpPost("master/insert")]
        public async Task<IActionResult> InsertMaster(
            [FromBody] DoctorAppointmentSlotMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.InsertMaster(data);
            return Ok(res);
        }

        [HttpPost("master/update")]
        public async Task<IActionResult> UpdateMaster(
            [FromBody] DoctorAppointmentSlotMasterModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.UpdateMaster(data);
            return Ok(res);
        }

        [HttpGet("master/delete")]
        public async Task<IActionResult> DeleteMaster(Guid slot_master_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.DeleteMaster(slot_master_id, tenant);
            return Ok(res);
        }

        // ═══════════════════════════════════════════
        // DETAILS ENDPOINTS
        // ═══════════════════════════════════════════

        [HttpGet("details/get")]
        public async Task<IActionResult> GetAllDetails()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetAllDetails(tenant);
            return Ok(data);
        }

        [HttpGet("details/get-by-date")]
        public async Task<IActionResult> GetDetailsByDate(int dcode, DateOnly appointment_date)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetDetailsByDate(dcode, appointment_date, tenant);
            return Ok(data);
        }

        [HttpPost("details/insert")]
        public async Task<IActionResult> InsertDetails(
            [FromBody] DoctorAppointmentSlotDetailsModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.InsertDetails(data);
            return Ok(res);
        }

        [HttpPost("details/update")]
        public async Task<IActionResult> UpdateDetails(
            [FromBody] DoctorAppointmentSlotDetailsModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.UpdateDetails(data);
            return Ok(res);
        }

        [HttpGet("details/delete")]
        public async Task<IActionResult> DeleteDetails(Guid slot_detail_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.DeleteDetails(slot_detail_id, tenant);
            return Ok(res);
        }

        [HttpGet("details/book-patient")]
        public async Task<IActionResult> BookPatient(Guid slot_detail_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.BookPatient(slot_detail_id, tenant);
            return Ok(res);
        }
       

        // ─────────────────────────────────────────
        // DETAILS BULK INSERT by date list
        // ─────────────────────────────────────────
        [HttpPost("details/bulk-insert")]
        public async Task<IActionResult> BulkInsertDetails(
            [FromBody] BulkInsertSlotDetailsRequest request)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.BulkInsertDetails(request, tenant);
            return Ok(res);
        }
    }
}