using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    public class OPRegistrationModel
    {
        [Table("op_registration")]
        public class OpRegistrationModel
        {
            [ExplicitKey]
            public Guid op_id { get; set; } = Guid.NewGuid();
            public string op_no { get; set; } = string.Empty;
            public Guid? booking_id { get; set; }
            public string? booking_no { get; set; }
            public Guid? slot_detail_id { get; set; }   // ✅ NEW — links to slot
            public decimal custid { get; set; }
            public int dcode { get; set; }
            public int? department_code { get; set; }
            public string visit_type { get; set; } = "NEWVISIT";
            public string reg_type { get; set; } = "WALKIN";    // ✅ NEW — WALKIN / ONLINE
            public DateOnly visit_date { get; set; }
            public int? token_no { get; set; }
            public int? queue_no { get; set; }
            public string visit_status { get; set; } = "WAITING";
            public string? notes { get; set; }
            public string? tenant_code { get; set; }
            public bool isdeleted { get; set; } = false;
            public DateTime created_at { get; set; } =
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            public DateTime updated_at { get; set; } =
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            public bool is_direct_walkin { get; set; } = false;
            public int? duty_dcode { get; set; }
            public int? transferred_to_dcode { get; set; }
            public string? transfer_reason { get; set; }
        }

        [Table("patient_vitals")]
        public class PatientVitalsModel
        {
            [ExplicitKey]
            public Guid vital_id { get; set; } = Guid.NewGuid();
            public Guid op_id { get; set; }
            public string op_no { get; set; } = string.Empty;
            public decimal custid { get; set; }
            public int dcode { get; set; }

            // ── Basic Vitals ─────────────────────────────
            public decimal? height_cm { get; set; }
            public decimal? weight_kg { get; set; }
            public decimal? bmi { get; set; }               // auto calculated
            public decimal? temperature_f { get; set; }
            public int? pulse_rate { get; set; }
            public int? respiratory_rate { get; set; }
            public int? bp_systolic { get; set; }
            public int? bp_diastolic { get; set; }
            public decimal? spo2 { get; set; }

            // ── Additional Measurements ───────────────────
            public decimal? sugar_level { get; set; }
            public int? pain_scale { get; set; }
            public decimal? waist_cm { get; set; }          // ✅ NEW — from old table
            public decimal? hip_cm { get; set; }            // ✅ NEW — from old table

            // ── Clinical Examination ──────────────────────
            public string? pedal_oedema { get; set; }       // ✅ NEW — from old table
            public string? jvp { get; set; }                // ✅ NEW — from old table
            public string? cvs { get; set; }                // ✅ NEW — cardiovascular system
            public string? rs { get; set; }                 // ✅ NEW — respiratory system
            public string? cns { get; set; }                // ✅ NEW — central nervous system
            public string? abdomen { get; set; }            // ✅ NEW — from old table

            // ── Investigations ────────────────────────────
            public string? cardiac_monitor { get; set; }    // ✅ NEW — from old table
            public string? cd_echo { get; set; }            // ✅ NEW — from old table
            public string? blood_chemistry { get; set; }    // ✅ NEW — from old table
            public string? allergy_notes { get; set; }

            // ── Special Dept ──────────────────────────────
            public decimal? hba1c { get; set; }
            public string? ecg_notes { get; set; }
            public decimal? head_circumference_cm { get; set; }

            public string? entered_by { get; set; }
            public string? tenant_code { get; set; }
            public bool isdeleted { get; set; } = false;
            public DateTime created_at { get; set; } =
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            public DateTime updated_at { get; set; } =
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        }

        public class UpdateVisitStatusRequest
        {
            public Guid op_id { get; set; }
            public string visit_status { get; set; } = string.Empty;
        }
        // Direct walk-in — no booking needed
        public class DirectWalkinRequest
        {
            public decimal custid { get; set; }
            public int? dcode { get; set; }           // null if patient doesn't know which doctor
            public int? duty_dcode { get; set; }      // assigned at reception if no dcode
            public int? department_code { get; set; }
            public Guid? slot_detail_id { get; set; }
            public string visit_type { get; set; } = "NEWVISIT";
            public string? notes { get; set; }
        }

        // Transfer to another doctor after duty doctor consultation
        public class TransferDoctorRequest
        {
            public Guid op_id { get; set; }
            public int transfer_to_dcode { get; set; }
            public string? transfer_reason { get; set; }
            public Guid? slot_detail_id { get; set; }
        }
        public class DoctorBookingListModel
        {
            public Guid booking_id { get; set; }
            public string? booking_no { get; set; }
            public decimal custid { get; set; }

            public string? patient_name { get; set; }

            public int dcode { get; set; }

            public DateOnly appointment_date { get; set; }

            public TimeOnly slot_start_time { get; set; }
            public TimeOnly slot_end_time { get; set; }

            public int token_no { get; set; }

            public string? booking_status { get; set; }
            public string? booking_type { get; set; }

            public string? notes { get; set; }
        }
    }
}
