using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("appointment_booking")]
    public class AppointmentBookingModel
    {
        [ExplicitKey]
        public Guid booking_id { get; set; } = Guid.NewGuid();
        public string? booking_no { get; set; }
        public decimal custid { get; set; }
        public int dcode { get; set; }
        public Guid slot_detail_id { get; set; }
        public Guid slot_master_id { get; set; }
        public DateOnly appointment_date { get; set; }
        public TimeOnly slot_start_time { get; set; }
        public TimeOnly slot_end_time { get; set; }
        public int token_no { get; set; } = 0;
        public string booking_status { get; set; } = "BOOKED";
        public string booking_type { get; set; } = "ONLINE";
        public Guid? rescheduled_from { get; set; }
        public string? reschedule_reason { get; set; }
        public string? cancel_reason { get; set; }
        public DateTime? cancelled_at { get; set; }
        public string? notes { get; set; }
        public string? tenant_code { get; set; }
        public bool isdeleted { get; set; } = false;
        public int usercode { get; set; } = 0;
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
    public class RescheduleSlotItemRequest
    {
        public Guid old_booking_id { get; set; }
        public string booking_type { get; set; } = "ONLINE";
        public string? reschedule_reason { get; set; }
        public Guid new_slot_detail_id { get; set; }
        public Guid new_slot_master_id { get; set; }
        public DateOnly new_appointment_date { get; set; }
        public TimeOnly new_slot_start_time { get; set; }
        public TimeOnly new_slot_end_time { get; set; }
        public int new_dcode { get; set; }
        public string? notes { get; set; }
    }

    // ✅ NEW — used by reschedule-whole-slot endpoint
    public class RescheduleWholeSlotRequest
    {
        public Guid slot_master_id { get; set; }
        public Guid new_slot_detail_id { get; set; }
        public Guid new_slot_master_id { get; set; }
        public DateOnly new_appointment_date { get; set; }
        public TimeOnly new_slot_start_time { get; set; }
        public TimeOnly new_slot_end_time { get; set; }
        public int new_dcode { get; set; }
        public string? reschedule_reason { get; set; }
    }
    public class AppointmentBookingViewModel
    {
        public Guid booking_id { get; set; }

        public decimal custid { get; set; }

        public string? customer_name { get; set; }

        public string? mobile { get; set; }

        public int dcode { get; set; }

        public DateOnly appointment_date { get; set; }

        public TimeOnly slot_start_time { get; set; }

        public TimeOnly slot_end_time { get; set; }

        public int token_no { get; set; }

        public string? booking_status { get; set; }

        public string? booking_type { get; set; }

        public string? tenant_code { get; set; }
    }
    public class AppointmentBookingLogModel
    {
        public Guid log_id { get; set; } = Guid.NewGuid();
        public Guid booking_id { get; set; }
        public string? booking_no { get; set; }
        public decimal custid { get; set; }
        public int dcode { get; set; }
        public string action { get; set; } = string.Empty;   // BOOKED / RESCHEDULED
        public string? action_by { get; set; }               // custid as string
        public Guid? old_slot_detail_id { get; set; }
        public Guid? new_slot_detail_id { get; set; }
        public DateOnly? old_appointment_date { get; set; }
        public DateOnly? new_appointment_date { get; set; }
        public TimeOnly? old_slot_start_time { get; set; }
        public TimeOnly? new_slot_start_time { get; set; }
        public string? remarks { get; set; }
        public string? tenant_code { get; set; }
        public DateTime created_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }

    public class PatientRescheduleRequest
    {
        public Guid old_booking_id { get; set; }
        public string booking_type { get; set; } = "ONLINE";
        public string? reschedule_reason { get; set; }
        public AppointmentBookingModel new_booking { get; set; } = new();
    }
    public class AppointmentBookingLogViewModel
    {
        public Guid log_id { get; set; }
        public Guid booking_id { get; set; }
        public string? booking_no { get; set; }
        public decimal custid { get; set; }
        public int dcode { get; set; }
        public string? doctor_name { get; set; }
        public string? action { get; set; }
        public string? action_by { get; set; }
        public string? booking_status { get; set; }
        public string? booking_type { get; set; }
        public int token_no { get; set; }
        public string? cancel_reason { get; set; }
        public DateTime? cancelled_at { get; set; }
        public Guid? rescheduled_from { get; set; }
        public Guid? old_slot_detail_id { get; set; }
        public DateOnly? old_appointment_date { get; set; }
        public TimeOnly? old_slot_start_time { get; set; }
        public Guid? new_slot_detail_id { get; set; }
        public DateOnly? new_appointment_date { get; set; }
        public TimeOnly? new_slot_start_time { get; set; }
        public string? remarks { get; set; }
        public DateTime created_at { get; set; }
        public string? tenant_code { get; set; }  // ✅ added
        public string? customer_name { get; set; }
        public string? mobile { get; set; }
    }
}