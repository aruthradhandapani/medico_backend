using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("test_master")]
    public class TestMasterModel
    {
        [ExplicitKey]
        public decimal tcode { get; set; }

        public decimal? gcode { get; set; }

        public decimal? scode { get; set; }

        public decimal? rtcode { get; set; }

        public decimal? ucode { get; set; }

        public decimal? rtmcode { get; set; }

        public decimal? orderno { get; set; }

        public string? name { get; set; }

        public string? shortname { get; set; }

        public string? qty { get; set; }

        public double amount { get; set; }

        public bool lockresult { get; set; }

        public bool? locksms { get; set; }

        public bool textcontent { get; set; }

        public bool? culturereport { get; set; }

        public bool? Routine { get; set; }

        public bool? outlab { get; set; }

        public string? description { get; set; }

        public string? footer { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }

        public bool? printinseparatepage { get; set; }

        public bool? printgraphinreport { get; set; }

        public string? graphtype { get; set; }

        public bool? istest { get; set; }

        public bool? ispackage { get; set; }

        public int? packcode { get; set; }

        public int? gstlcode { get; set; }

        public double? gstper { get; set; }

        public double? gstamount { get; set; }

        public string? hsn { get; set; }

        public string? hsndescription { get; set; }

        public bool isnodiscount { get; set; }

        public string? ccf { get; set; }

        public double? ccv { get; set; }

        public string? csf { get; set; }

        public double? CSV { get; set; }

        public string? hsf { get; set; }

        public double? hsv { get; set; }

        public bool? isccv { get; set; }

        public bool? iscsv { get; set; }

        public bool? isncv { get; set; }

        public string? ncf { get; set; }

        public double? ncv { get; set; }

        public string? tcf { get; set; }

        public string? tenant_code { get; set; }
    }
}