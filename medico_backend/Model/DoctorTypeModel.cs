using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("doctor_type_master")]
    public class DoctorTypeMasterModel
    {
        [ExplicitKey]
        public int tcode { get; set; }

        public string? tenant_code { get; set; }

        public int orderno { get; set; }

        public string? name { get; set; }

        public string? shortname { get; set; }

        public string? description { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }
    }
}