using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    [Table("mastertenant.tenants")]
    public class TenantModel
    {
        public int id { get; set; }

        [ExplicitKey]
        public Guid tenant_id { get; set; } = Guid.NewGuid();

        public string tenant_code { get; set; } = string.Empty;

        public string tenant_name { get; set; } = string.Empty;

        public string? connection_string { get; set; }

        public bool is_active { get; set; } = true;

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        public string? logo_url { get; set; }
        public string? host_url { get; set; }
        public string? api_url { get; set; }
        public string? contact_email { get; set; }
        public string? contact_number { get; set; }
        public string? legal_name { get; set; }
        public string? contact_person { get; set; }
        public string? alternate_mobile { get; set; }
        public string? gst_number { get; set; }
        public string? pan_number { get; set; }
        public string? address_line1 { get; set; }
        public string? address_line2 { get; set; }
        public string? city { get; set; }
        public string? state { get; set; }
        public string? country { get; set; }
        public string? pincode { get; set; }
        public string? time_zone { get; set; } = "Asia/Kolkata";
        public string? currency { get; set; } = "INR";
        public string? business_type { get; set; }
        public string? register_num { get; set; }

        public bool isdeleted { get; set; } = false;

        [Write(false)]
        public string? password_hash { get; set; }

        [Write(false)]
        public string? reset_token { get; set; }

        [Write(false)]
        public DateTime? reset_token_expiry { get; set; }

        [Write(false)]
        public string? reset_otp { get; set; }

        [Write(false)]
        public DateTime? reset_otp_expiry { get; set; }
    }


    public class UpdateTenantRequest
    {
        public Guid tenant_id { get; set; }
        public string tenant_name { get; set; } = string.Empty;
        public string? connection_string { get; set; }
        public bool is_active { get; set; } = true;
        public string? logo_url { get; set; }
        public string? host_url { get; set; }
        public string? api_url { get; set; }
        public string? contact_email { get; set; }
        public string? contact_number { get; set; }
        public string? legal_name { get; set; }
        public string? contact_person { get; set; }
        public string? alternate_mobile { get; set; }
        public string? gst_number { get; set; }
        public string? pan_number { get; set; }
        public string? address_line1 { get; set; }
        public string? address_line2 { get; set; }
        public string? city { get; set; }
        public string? state { get; set; }
        public string? country { get; set; }
        public string? pincode { get; set; }
        public string? time_zone { get; set; }
        public string? currency { get; set; }
        public string? business_type { get; set; }
        public string? register_num { get; set; }
    }
}