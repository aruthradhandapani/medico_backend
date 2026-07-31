using System;
using System.Collections.Generic;

namespace medico_backend.Model
{
    public class DischargeSummaryModel
    {
        public class ds_category
        {
            public Guid category_id { get; set; }
            public string category_name { get; set; } = string.Empty;
            public string category_type { get; set; } = "TEXT";
            public int sort_order { get; set; } = 0;
            public string tenant_code { get; set; } = string.Empty;
            public bool is_active { get; set; } = true;
            public DateTime created_at { get; set; } = DateTime.UtcNow;
        }

        public class ds_template
        {
            public Guid template_id { get; set; }
            public string template_name { get; set; } = string.Empty;
            public Guid category_id { get; set; }
            public string category_name { get; set; } = string.Empty;
            public string template_text { get; set; } = string.Empty;
            public string tenant_code { get; set; } = string.Empty;
            public string? created_by { get; set; }
            public DateTime created_at { get; set; } = DateTime.UtcNow;
        }

        public class pds_master
        {
            public Guid pds_id { get; set; }
            public string patcode { get; set; } = string.Empty;
            public long custid { get; set; }
            public Guid? op_id { get; set; }
            public int? dcode { get; set; }
            public string? patient_name { get; set; }
            public string? gender { get; set; }
            public string? age { get; set; }
            public string? mobile_no { get; set; }
            public string? doctor_name { get; set; }
            public string? bed_no { get; set; }
            public DateTime? admission_date { get; set; }
            public DateTime? discharge_date { get; set; }
            public string? discharge_type { get; set; } = "NORMAL";
            public string? overall_notes { get; set; }
            public string? auth_user1 { get; set; }
            public string? auth_user2 { get; set; }
            public string? auth_user3 { get; set; }
            public string tenant_code { get; set; } = string.Empty;
            public string? created_by { get; set; }
            public DateTime created_at { get; set; } = DateTime.UtcNow;
            public DateTime updated_at { get; set; } = DateTime.UtcNow;
            public bool is_deleted { get; set; } = false;
        }

        public class pds_detail
        {
            public Guid pds_detail_id { get; set; }
            public Guid pds_id { get; set; }
            public Guid category_id { get; set; }
            public string category_name { get; set; } = string.Empty;
            public string category_content { get; set; } = string.Empty;
            public int sort_order { get; set; } = 0;
            public string tenant_code { get; set; } = string.Empty;
        }

        // DTOs
        public class SaveCategoryDto
        {
            public Guid? category_id { get; set; }
            public string category_name { get; set; } = string.Empty;
            public string category_type { get; set; } = "TEXT";
            public int sort_order { get; set; } = 0;
            public bool is_active { get; set; } = true;
        }

        public class SaveTemplateDto
        {
            public Guid? template_id { get; set; }
            public string template_name { get; set; } = string.Empty;
            public Guid category_id { get; set; }
            public string category_name { get; set; } = string.Empty;
            public string template_text { get; set; } = string.Empty;
        }

        public class SavePatientDischargeSummaryDto
        {
            public Guid? pds_id { get; set; }
            public string patcode { get; set; } = string.Empty;
            public long custid { get; set; }
            public Guid? op_id { get; set; }
            public int? dcode { get; set; }
            public string? patient_name { get; set; }
            public string? gender { get; set; }
            public string? age { get; set; }
            public string? mobile_no { get; set; }
            public string? doctor_name { get; set; }
            public string? bed_no { get; set; }
            public DateTime? admission_date { get; set; }
            public DateTime? discharge_date { get; set; }
            public string? discharge_type { get; set; } = "NORMAL";
            public string? overall_notes { get; set; }
            public string? auth_user1 { get; set; }
            public string? auth_user2 { get; set; }
            public string? auth_user3 { get; set; }
            public List<PdsCategoryContentDto> details { get; set; } = new();
        }

        public class PdsCategoryContentDto
        {
            public Guid category_id { get; set; }
            public string category_name { get; set; } = string.Empty;
            public string category_content { get; set; } = string.Empty;
            public int sort_order { get; set; } = 0;
        }

        public class AuthorizeDischargeSummaryDto
        {
            public Guid pds_id { get; set; }
            public string? auth_user1 { get; set; }
            public string? auth_user2 { get; set; }
            public string? auth_user3 { get; set; }
        }

        public class PatientDischargeSummaryResponse
        {
            public pds_master Master { get; set; } = new();
            public List<pds_detail> Details { get; set; } = new();
            public string PatientName { get; set; } = string.Empty;
            public string PatientId { get; set; } = string.Empty;
            public string Gender { get; set; } = string.Empty;
            public string Age { get; set; } = string.Empty;
            public string MobileNo { get; set; } = string.Empty;
            public string DoctorName { get; set; } = string.Empty;
            public string BedNo { get; set; } = string.Empty;
        }
    }
}
