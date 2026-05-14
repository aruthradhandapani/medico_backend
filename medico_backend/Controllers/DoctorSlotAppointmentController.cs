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

        // ✅ UPDATE MASTER
        // DETAILS AUTO UPDATE
        [HttpPost("master/update")]
        public async Task<IActionResult> UpdateMaster(
            [FromBody]
            DoctorAppointmentSlotMasterModel data)
        {
            var tenant =
                Request.Headers["tenant_code"].ToString();

            data.tenant_code = tenant;

            return Ok(
                await cls.UpdateMaster(data));
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

    }
}