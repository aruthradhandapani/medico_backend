using Microsoft.AspNetCore.Mvc;
using Medico_Backend.Class;
using Medico_Backend.Model;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentBookingController : ControllerBase
    {
        private readonly AppointmentBookingClass cls;

        public AppointmentBookingController(AppointmentBookingClass _cls)
        {
            cls = _cls;
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAll()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetAll(tenant);
            return Ok(data);
        }

        [HttpGet("get-by-date")]
        public async Task<IActionResult> GetByDate(DateOnly appointment_date)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetByDate(appointment_date, tenant);
            return Ok(data);
        }

        [HttpGet("get-available-slots")]
        public async Task<IActionResult> GetAvailableSlots(int dcode, DateOnly appointment_date)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetAvailableSlots(dcode, appointment_date, tenant);
            return Ok(data);
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetTodayAppointments(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetTodayAppointments(dcode, tenant);
            return Ok(data);
        }

        [HttpGet("by-customer")]
        public async Task<IActionResult> GetByCustomer(decimal custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetByCustomer(custid, tenant);
            return Ok(data);
        }

        [HttpGet("customer-info")]
        public async Task<IActionResult> GetCustomerInfo(decimal custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetCustomerInfo(custid, tenant);
            return Ok(data);
        }

        [HttpPost("book")]
        public async Task<IActionResult> Book([FromBody] AppointmentBookingModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.BookAppointment(data);
            return Ok(res);
        }

        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel([FromBody] CancelAppointmentRequest request)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.CancelAppointment(
                request.booking_id, request.cancel_reason, tenant);
            return Ok(res);
        }

        [HttpPost("reschedule")]
        public async Task<IActionResult> Reschedule(
            [FromBody] RescheduleAppointmentRequest request)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.RescheduleAppointment(request, tenant);
            return Ok(res);
        }

        // ✅ Fixed — PATCH is correct for status updates, not GET
        [HttpPost  ("update-status")]
        public async Task<IActionResult> UpdateStatus(Guid booking_id, string booking_status)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.UpdateStatus(booking_id, booking_status, tenant);
            return Ok(res);
        }
        // ✅ Reschedule ALL patients from a cancelled slot into one new slot
        // ✅ Whole slot reschedule — doctor cancelled, move all patients
        [HttpPost("reschedule-whole-slot")]
        public async Task<IActionResult> RescheduleWholeSlot(
    [FromBody] RescheduleWholeSlotRequest request)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            if (request.slot_master_id == Guid.Empty)
                return BadRequest("slot_master_id is required");

            if (request.new_slot_detail_id == Guid.Empty)
                return BadRequest("new_slot_detail_id is required");

            if (request.new_appointment_date == default)
                return BadRequest("new_appointment_date is required");

            try
            {
                var result = await cls.RescheduleWholeSlot(request, tenant);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }
        [HttpPost("patient-reschedule")]
        public async Task<IActionResult> PatientReschedule(
    [FromBody] PatientRescheduleRequest request)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.PatientReschedule(request, tenant);
            return Ok(res);
        }
        // ─────────────────────────────────────────────────────────────────
        //  GET APPOINTMENT LOG
        //
        //  Modes:
        //    by booking_id  → full trail of one booking (booked → rescheduled → cancelled)
        //    by custid      → all activity for a patient (used by chat app to show context)
        //    by dcode+date  → doctor's action log for a day
        //
        //  All query params are optional — combine freely.
        //
        //  GET /api/AppointmentBooking/log?custid=1001
        //  GET /api/AppointmentBooking/log?booking_id=guid-here
        //  GET /api/AppointmentBooking/log?dcode=5&from_date=2026-06-01&to_date=2026-06-19
        //  GET /api/AppointmentBooking/log?custid=1001&action_filter=RESCHEDULED
        // ─────────────────────────────────────────────────────────────────
        [HttpGet("log")]
        public async Task<IActionResult> GetLog(
            [FromQuery] Guid? booking_id = null,
            [FromQuery] decimal? custid = null,
            [FromQuery] int? dcode = null,
            [FromQuery] DateOnly? from_date = null,
            [FromQuery] DateOnly? to_date = null,
            [FromQuery] string? action_filter = null)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var data = await cls.GetAppointmentLog(
                tenant, booking_id, custid, dcode, from_date, to_date, action_filter);

            return Ok(new
            {
                count = data.Count,
                data
            });
        }
    }
}