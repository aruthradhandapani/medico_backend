using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("doctor_group_master")]
    public class DoctorGroupMasterModel
    {
        [Key]
        public long group_id { get; set; }

        public string group_name { get; set; } = "";
        public string? short_name { get; set; }
        public string? description { get; set; }

        // "GROUP" = shared token across all doctors in the group
        // "DOCTOR" = each doctor gets their own independent token sequence
        public string token_type { get; set; } = "GROUP";

        public int display_order { get; set; } = 1;

        public bool is_active { get; set; } = true;
        public bool is_deleted { get; set; } = false;

        public string? tenant_code { get; set; }

        public int usercode { get; set; } = 1;
        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }
        public DateTime ibsdate { get; set; }
    }
}