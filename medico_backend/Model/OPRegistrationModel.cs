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

            // Basic Vitals
            public decimal? height_cm { get; set; }
            public decimal? weight_kg { get; set; }
            public decimal? bmi { get; set; }               // auto calculated
            public decimal? temperature_f { get; set; }
            public int? pulse_rate { get; set; }
            public int? respiratory_rate { get; set; }
            public int? bp_systolic { get; set; }
            public int? bp_diastolic { get; set; }
            public decimal? spo2 { get; set; }

            // Additional
            public decimal? sugar_level { get; set; }
            public int? pain_scale { get; set; }
            public string? allergy_notes { get; set; }

            // Special dept
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
    }
}
