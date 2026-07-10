using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    public class IPRegistrationModel
    {
        [Table("ip_registration")]
        public class IpRegistrationModel
        {
            [ExplicitKey]
            public Guid ip_id { get; set; } = Guid.NewGuid();

            public string ip_no { get; set; } = string.Empty;

            public decimal custid { get; set; }
            public Guid? booking_id { get; set; }
            public Guid? op_id { get; set; }

            public int dcode { get; set; }
            public int? referring_dcode { get; set; }
            public int? department_code { get; set; }

            public string admission_type { get; set; } = "PLANNED";
            public string? admission_reason { get; set; }
            public DateTime admitdate { get; set; }
            public DateTime? expected_dischargedate { get; set; }
            public DateTime? dischargedate { get; set; }
            public string? discharge_summary { get; set; }
            public string? discharge_type { get; set; }

            public int? branchcode { get; set; }
            public int? blockcode { get; set; }
            public int? flrcode { get; set; }
            public int? wrdcode { get; set; }
            public int? rmtcode { get; set; }
            public int? bedcode { get; set; }

            public string ip_status { get; set; } = "ADMITTED";

            public bool isinsurancepatient { get; set; } = false;
            public string? insurance_company { get; set; }
            public string? policyno { get; set; }
            public string? authorizationno { get; set; }
            public string? tpa_name { get; set; }
            public double? insurance_approved_amount { get; set; }
            public string? insurance_status { get; set; }

            public string? guardian_name { get; set; }
            public string? guardian_relation { get; set; }
            public string? guardian_contact { get; set; }

            public string? notes { get; set; }

            public string? tenant_code { get; set; }
            public bool isdeleted { get; set; } = false;
            public int? usercode { get; set; } = 1;
            public int? computercode { get; set; } = 1;
            public DateTime created_at { get; set; } =
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            public DateTime updated_at { get; set; } =
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        }

        // ─── Request DTOs ──────────────────────────────
        public class CreateIpRegistrationRequest
        {
            public decimal custid { get; set; }
            public Guid? booking_id { get; set; }
            public Guid? op_id { get; set; }
            public int dcode { get; set; }
            public int? referring_dcode { get; set; }
            public int? department_code { get; set; }
            public string admission_type { get; set; } = "PLANNED";
            public string? admission_reason { get; set; }
            public DateTime? admitdate { get; set; }
            public DateTime? expected_dischargedate { get; set; }

            public int? branchcode { get; set; }
            public int? blockcode { get; set; }
            public int? flrcode { get; set; }
            public int? wrdcode { get; set; }
            public int? rmtcode { get; set; }
            public int bedcode { get; set; }        // required — must allocate a bed on admission

            public bool isinsurancepatient { get; set; } = false;
            public string? insurance_company { get; set; }
            public string? policyno { get; set; }
            public string? authorizationno { get; set; }
            public string? tpa_name { get; set; }
            public double? insurance_approved_amount { get; set; }

            public string? guardian_name { get; set; }
            public string? guardian_relation { get; set; }
            public string? guardian_contact { get; set; }

            public string? notes { get; set; }
        }

        public class DischargeRequest
        {
            public Guid ip_id { get; set; }
            public string discharge_type { get; set; } = "NORMAL";
            public string? discharge_summary { get; set; }
        }

        // ─── UPDATE — IP/admission details ONLY. No bed/room fields.
        //     Room/bed changes must go through BedTransferController → /insert.
        public class UpdateIpRegistrationRequest
        {
            public Guid ip_id { get; set; }
            public decimal custid { get; set; }
            public Guid? booking_id { get; set; }
            public Guid? op_id { get; set; }
            public int dcode { get; set; }
            public int? referring_dcode { get; set; }
            public int? department_code { get; set; }
            public string admission_type { get; set; } = "PLANNED";
            public string? admission_reason { get; set; }
            public DateTime? admitdate { get; set; }
            public DateTime? expected_dischargedate { get; set; }

            public bool isinsurancepatient { get; set; } = false;
            public string? insurance_company { get; set; }
            public string? policyno { get; set; }
            public string? authorizationno { get; set; }
            public string? tpa_name { get; set; }
            public double? insurance_approved_amount { get; set; }
            public string? insurance_status { get; set; }

            public string? guardian_name { get; set; }
            public string? guardian_relation { get; set; }
            public string? guardian_contact { get; set; }

            public string? notes { get; set; }
        }

        public class CancelAdmissionRequest
        {
            public Guid ip_id { get; set; }
            public string? reason { get; set; }
        }
    }
}