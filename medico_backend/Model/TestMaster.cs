using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("test_master")]
    public class TestMasterModel
    {
        [Key]  // auto-increment
        public long tcode { get; set; }

        public long gcode { get; set; }
        public long scode { get; set; }
        public long rtcode { get; set; }
        public long ucode { get; set; }
        public long rtmcode { get; set; }
        public int orderno { get; set; }

        public string name { get; set; } = string.Empty;
        public string shortname { get; set; } = string.Empty;
        public string qty { get; set; } = string.Empty;
        public double amount { get; set; }

        // Replaces: textcontent, culturereport, Routine, outlab
        public int ttid { get; set; }

        public bool lockresult { get; set; }
        public bool locksms { get; set; }
        public string? description { get; set; }
        public string? footer { get; set; }
        public bool? deleted { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTimeOffset? entereddate { get; set; }
        public DateTimeOffset? ibsdate { get; set; }
        public bool printinseparatepage { get; set; }
        public bool printgraphinreport { get; set; }
        public string? graphtype { get; set; }
        public long? cgcode { get; set; }
        public bool? istest { get; set; }
        public bool? ispackage { get; set; }
        public int? packcode { get; set; }
        public string? skycode { get; set; }
        public bool? iscontrast { get; set; }
        public bool? isnoic { get; set; }
        public string? tenant_code { get; set; }
        public decimal? tax_rate { get; set; }
        public string? icd_code { get; set; }

        [Computed]
        public string? test_type_name { get; set; }
    }
}