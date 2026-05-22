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

        public DoctorAppointmentSlotController(
            DoctorAppointmentSlotClass _cls)
        {
            cls = _cls;
        }

        // ═══════════════════════════════════════════
        // MASTER
        // ═══════════════════════════════════════════

        // ✅ GET ALL MASTER
        [HttpGet("master/get")]
        public async Task<IActionResult> GetAllMaster()
        {
            var tenant =
                Request.Headers["tenant_code"].ToString();

            return Ok(
                await cls.GetAllMaster(tenant));
        }

        // ✅ GET MASTER BY DOCTOR
        [HttpGet("master/get-by-doctor")]
        public async Task<IActionResult> GetMasterByDoctor(
            int dcode)
        {
            var tenant =
                Request.Headers["tenant_code"].ToString();

            return Ok(
                await cls.GetMasterByDoctor(
                    dcode,
                    tenant));
        }

        // ✅ BULK INSERT MASTER
        // DETAILS AUTO INSERTS
        [HttpPost("master/insert")]
        public async Task<IActionResult> BulkInsertMaster(
            [FromBody]
            List<DoctorAppointmentSlotMasterModel> data)
        {
            var tenant =
                Request.Headers["tenant_code"].ToString();

            var result =
                await cls.BulkInsertMaster(
                    data,
                    tenant);

            return Ok(result);
        }

        [HttpPost("master/update")]
        public async Task<IActionResult> BulkUpdateMaster(
    [FromBody] List<DoctorAppointmentSlotMasterModel> data)
        {
            var tenant =
                Request.Headers["tenant_code"].ToString();

            var result =
                await cls.BulkUpdateMaster(data, tenant);

            return Ok(result);
        }

        // ✅ DELETE MASTER
        // DETAILS AUTO DELETE
        [HttpGet("master/delete")]
        public async Task<IActionResult> DeleteMaster(
            Guid slot_master_id)
        {
            var tenant =
                Request.Headers["tenant_code"].ToString();

            return Ok(
                await cls.DeleteMaster(
                    slot_master_id,
                    tenant));
        }

        // ═══════════════════════════════════════════
        // DETAILS
        // ═══════════════════════════════════════════

        // ✅ GET ALL DETAILS
        [HttpGet("details/get")]
        public async Task<IActionResult> GetAllDetails()
        {
            var tenant =
                Request.Headers["tenant_code"].ToString();

            return Ok(
                await cls.GetAllDetails(tenant));
        }

        // ✅ GET DETAILS BY DATE
        [HttpGet("details/get-by-date")]
        public async Task<IActionResult> GetDetailsByDate(
            int dcode,
            DateOnly appointment_date)
        {
            var tenant =
                Request.Headers["tenant_code"].ToString();

            return Ok(
                await cls.GetDetailsByDate(
                    dcode,
                    appointment_date,
                    tenant));
        }

        [HttpPost("master/cancel")]
        public async Task<IActionResult> CancelSlot(
    Guid slot_master_id,
    string cancel_reason)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            if (string.IsNullOrWhiteSpace(tenant))
                return BadRequest("tenant_code is required");

            var result = await cls.CancelSlot(
                slot_master_id,
                tenant,
                cancel_reason);

            return Ok(result);
        }

        // ✅ GET DETAILS BY MASTER — useful to see all dates a master slot covers
        [HttpGet("details/get-by-master")]
        public async Task<IActionResult> GetDetailsByMaster(Guid slot_master_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.GetDetailsByMaster(slot_master_id, tenant));
        }

        // ✅ GET SINGLE DETAIL — needed before booking to verify status
        [HttpGet("details/get-by-id")]
        public async Task<IActionResult> GetDetailById(Guid slot_detail_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.GetDetailById(slot_detail_id, tenant));
        }

    }
}