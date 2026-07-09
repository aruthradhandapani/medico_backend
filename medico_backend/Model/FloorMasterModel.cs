using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("public.floor_master")]
    public class FloorMasterModel
    {
        [Key]
        public int flrcode { get; set; }

        public int orderno { get; set; }

        public string? name { get; set; }

        public string? shortname { get; set; }

        public string? description { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }

        public int? branchcode { get; set; }

        public string? tenant_code { get; set; }

        public int? blockcode { get; set; }
    }
}