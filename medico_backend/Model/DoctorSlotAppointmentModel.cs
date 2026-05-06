using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Dapper.Contrib.Extensions.Table("doctor_appointment_slot_master")]
    public class DoctorAppointmentSlotMasterModel
    {
        [ExplicitKey]
        public Guid slot_master_id { get; set; } = Guid.NewGuid();
        public int dcode { get; set; }
        public string? tenant_code { get; set; }
        public string day_of_week { get; set; } = string.Empty;
        public TimeOnly slot_start_time { get; set; }
        public TimeOnly slot_end_time { get; set; }
        public int max_patients { get; set; } = 10;
        public bool is_active { get; set; } = true;
        public bool isdeleted { get; set; } = false;

        // ✅ Always store as UTC — no offset ambiguity
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime updated_at { get; set; } = DateTime.UtcNow;
        public int? max_walkin { get; set; }
        public int? max_online { get; set; }
        public DateOnly? slot_date { get; set; }
    }

    [Dapper.Contrib.Extensions.Table("doctor_appointment_slot_details")]
    public class DoctorAppointmentSlotDetailsModel
    {
        [ExplicitKey]
        public Guid slot_detail_id { get; set; } = Guid.NewGuid();
        public Guid slot_master_id { get; set; }
        public int dcode { get; set; }
        public string? tenant_code { get; set; }
        public DateOnly appointment_date { get; set; }
        public TimeOnly slot_start_time { get; set; }
        public TimeOnly slot_end_time { get; set; }
        public int max_patients { get; set; } = 10;
        public int booked_count { get; set; } = 0;
        public string slot_status { get; set; } = "OPEN";
        public bool is_active { get; set; } = true;
        public bool isdeleted { get; set; } = false;

        // ✅ Always store as UTC
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime updated_at { get; set; } = DateTime.UtcNow;
    }
}