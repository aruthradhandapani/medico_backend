using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("appointment_booking")]
    public class AppointmentBookingModel
    {
        [ExplicitKey]
        public Guid booking_id { get; set; } = Guid.NewGuid();
        public decimal custid { get; set; }
        public int dcode { get; set; }
        public Guid slot_detail_id { get; set; }
        public Guid slot_master_id { get; set; }
        public DateOnly appointment_date { get; set; }
        public TimeOnly slot_start_time { get; set; }
        public TimeOnly slot_end_time { get; set; }
        public int token_no { get; set; } = 0;
        // BOOKED, CONFIRMED, VISITED, CANCELLED, RESCHEDULED
        public string booking_status { get; set; } = "BOOKED";
        // ✅ WALKIN or ONLINE
        public string booking_type { get; set; } = "ONLINE";
        public Guid? rescheduled_from { get; set; }
        public string? reschedule_reason { get; set; }
        public string? cancel_reason { get; set; }
        public DateTime? cancelled_at { get; set; }
        public string? notes { get; set; }
        public string? tenant_code { get; set; }
        public bool isdeleted { get; set; } = false;
        public DateTime created_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public DateTime updated_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }

    public class CancelAppointmentRequest
    {
        public Guid booking_id { get; set; }
        public string cancel_reason { get; set; } = string.Empty;
    }

    public class RescheduleAppointmentRequest
    {
        public Guid old_booking_id { get; set; }
        // ✅ booking_type needed to correctly decrement old slot counters
        public string booking_type { get; set; } = "ONLINE";
        public string? reschedule_reason { get; set; }
        public AppointmentBookingModel new_booking { get; set; } = new();
    }
}