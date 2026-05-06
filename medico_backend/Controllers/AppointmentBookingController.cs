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

        // Step 1: Get available slots
        [HttpGet("get-available-slots")]
        public async Task<IActionResult> GetAvailableSlots(int dcode, DateOnly appointment_date)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetAvailableSlots(dcode, appointment_date, tenant);
            return Ok(data);
        }

        // Step 2: Book appointment
        [HttpPost("book")]
        public async Task<IActionResult> Book([FromBody] AppointmentBookingModel data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            data.tenant_code = tenant;
            var res = await cls.BookAppointment(data);
            return Ok(res);
        }

        // Cancel appointment
        [HttpGet("cancel")]
        public async Task<IActionResult> Cancel(Guid booking_id, string cancel_reason)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.CancelAppointment(booking_id, cancel_reason, tenant);
            return Ok(res);
        }

        // Reschedule appointment
        [HttpPost("reschedule")]
        public async Task<IActionResult> Reschedule(
            Guid old_booking_id, [FromBody] AppointmentBookingModel new_data)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            new_data.tenant_code = tenant;
            var res = await cls.RescheduleAppointment(old_booking_id, new_data);
            return Ok(res);
        }

        // Update status → CONFIRMED / VISITED
        [HttpGet("update-status")]
        public async Task<IActionResult> UpdateStatus(Guid booking_id, string booking_status)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.UpdateStatus(booking_id, booking_status, tenant);
            return Ok(res);
        }

        // Today's appointments by doctor
        [HttpGet("today")]
        public async Task<IActionResult> GetTodayAppointments(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetTodayAppointments(dcode, tenant);
            return Ok(data);
        }

        // Appointment history by customer
        [HttpGet("by-customer")]
        public async Task<IActionResult> GetByCustomer(decimal custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetByCustomer(custid, tenant);
            return Ok(data);
        }

        // Get customer info from customer DB
        [HttpGet("customer-info")]
        public async Task<IActionResult> GetCustomerInfo(decimal custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetCustomerInfo(custid, tenant);
            return Ok(data);
        }
    }
}