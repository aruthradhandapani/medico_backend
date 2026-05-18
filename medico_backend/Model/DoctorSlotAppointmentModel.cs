using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("doctor_appointment_slot_master")]
    public class DoctorAppointmentSlotMasterModel
    {
        [ExplicitKey]
        public Guid slot_master_id { get; set; } = Guid.NewGuid();
        public int slotnum { get; set; }
        public int dcode { get; set; }
        public string? tenant_code { get; set; }

        public int? avgtime { get; set; }

        public string? day_of_week { get; set; }
        public TimeOnly slot_start_time { get; set; }
        public TimeOnly slot_end_time { get; set; }
        public string? typeofslot { get; set; }
        public int max_patients { get; set; } = 10;
        public int max_walkin { get; set; } = 5;
        public int max_online { get; set; } = 5;

        public DateOnly? slot_date { get; set; }
        public bool is_active { get; set; } = true;
        public bool isdeleted { get; set; } = false;
        public DateTime created_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public DateTime updated_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public bool is_cancel { get; set; } = false;
        public string? cancel_reason { get; set; }

    }

    [Table("doctor_appointment_slot_details")]
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

        // ── Capacity ───────────────────────────────────────────────
        public int max_patients { get; set; } = 10;
        public int max_walkin { get; set; } = 5;
        public int max_online { get; set; } = 5;
        // ── Live Counters ──────────────────────────────────────────
        public int booked_count { get; set; } = 0;
        public int walkin_count { get; set; } = 0;
        public int online_count { get; set; } = 0;
        // ── Computed (from SQL — not a DB column) ──────────────────
        [Computed]
        public int remaining_seats { get; set; }
        // OPEN, FULL, CLOSED, CANCELLED
        public string slot_status { get; set; } = "OPEN";
        public bool is_active { get; set; } = true;
        public bool isdeleted { get; set; } = false;
        public DateTime created_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public DateTime updated_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }
    [Table("doctor_appointment_slot_type")]
    public class DoctorAppointmentSlotTypeModel
    {
        [ExplicitKey]
        public long slot_type_id { get; set; }

        public string? name { get; set; }

        public string? shortname { get; set; }

        public string? colorcode { get; set; }

        public string? description { get; set; }

        public DateTime entereddate { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        public DateTime ibsdate { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        public bool deleted { get; set; } = false;

        public string? tenant_code { get; set; }
        public bool is_visiting { get; set; } = false;

        public string? emoji { get; set; }
    }

    // ✅ Used for bulk insert of slot details from a master
    public class BulkInsertSlotDetailsRequest
    {
        public Guid slot_master_id { get; set; }
        public int dcode { get; set; }
        public List<DateOnly> appointment_dates { get; set; } = new();
        public TimeOnly slot_start_time { get; set; }
        public TimeOnly slot_end_time { get; set; }
        public int max_patients { get; set; } = 10;
        public int max_walkin { get; set; } = 5;
        public int max_online { get; set; } = 5;
        public bool is_active { get; set; } = true;
    }
    public class DoctorAppointmentSlotMasterBulkModel
    {
        public List<DoctorAppointmentSlotMasterModel> Slots { get; set; } = new();
    }
}