using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorAppointmentSlotTypeController
        : ControllerBase
    {
        private readonly
            DoctorAppointmentSlotTypeClass cls;

        public DoctorAppointmentSlotTypeController(
            DoctorAppointmentSlotTypeClass _cls)
        {
            cls = _cls;
        }

        // ═══════════════════════════════════════
        // GET ALL
        // ═══════════════════════════════════════
        [HttpGet("get")]
        public async Task<IActionResult> GetAll()
        {
            var tenant =
                Request.Headers["tenant_code"]
                    .ToString();

            return Ok(
                await cls.GetAll(tenant));
        }

        // ═══════════════════════════════════════
        // GET BY ID
        // ═══════════════════════════════════════
        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetById(
            long slot_type_id)
        {
            var tenant =
                Request.Headers["tenant_code"]
                    .ToString();

            return Ok(
                await cls.GetById(
                    slot_type_id,
                    tenant));
        }

        // ═══════════════════════════════════════
        // INSERT
        // ═══════════════════════════════════════
        [HttpPost("insert")]
        public async Task<IActionResult> Insert(
            [FromBody]
            DoctorAppointmentSlotTypeModel data)
        {
            var tenant =
                Request.Headers["tenant_code"]
                    .ToString();

            data.tenant_code = tenant;

            return Ok(
                await cls.Insert(data));
        }

        // ═══════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════
        [HttpPost("update")]
        public async Task<IActionResult> Update(
            [FromBody]
            DoctorAppointmentSlotTypeModel data)
        {
            var tenant =
                Request.Headers["tenant_code"]
                    .ToString();

            data.tenant_code = tenant;

            return Ok(
                await cls.Update(data));
        }

        // ═══════════════════════════════════════
        // DELETE
        // ═══════════════════════════════════════
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(
            long slot_type_id)
        {
            var tenant =
                Request.Headers["tenant_code"]
                    .ToString();

            return Ok(
                await cls.Delete(
                    slot_type_id,
                    tenant));
        }
    }
}