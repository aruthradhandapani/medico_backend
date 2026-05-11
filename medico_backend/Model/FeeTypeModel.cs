using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("feetype_master")]
    public class FeeTypeMasterModel
    {
        [ExplicitKey]
        public int ftcode { get; set; }

        public int orderno { get; set; }

        public string? shortname { get; set; }

        public string? name { get; set; }

        public string? description { get; set; }

        public double? commissionpercentage { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }

        public string? tenant_code { get; set; }
    }
}