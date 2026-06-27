using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("test_type_master")]
    public class TestTypeMasterModel
    {
        [Key]
        public long ttid { get; set; }

        public string? tenant_code { get; set; }

        public string shortname { get; set; } = string.Empty;

        public string name { get; set; } = string.Empty;

        public bool deleted { get; set; } = false;

        public int? usercode { get; set; }

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }
    }
}