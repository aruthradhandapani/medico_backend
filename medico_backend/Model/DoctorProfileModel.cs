using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("doctor_profile")]
    public class DoctorProfileModel
    {
        [Key]
        public int pcode { get; set; }

        public int dcode { get; set; }

        public string? about { get; set; }

        public double? experience_years { get; set; }

        public string? education_details { get; set; }

        public string? operations_performed { get; set; }

        public int? patients_treated { get; set; }

        public string? achievements { get; set; }

        public string? memberships { get; set; }

        public string? publications { get; set; }

        public string? languages_known { get; set; }

        public string? profile_video_url { get; set; }

        public string? banner_image { get; set; }

        public int? orderno { get; set; }

        public bool is_published { get; set; } = true;

        public bool deleted { get; set; } = false;

        public int? usercode { get; set; } = 1;

        public int? computercode { get; set; } = 1;

        public DateTime? entereddate { get; set; }

        public DateTime? ibsdate { get; set; }

        public string? tenant_code { get; set; }
    }
}