using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("counter_master")]
    public class CounterMasterModel
    {
        [ExplicitKey]
        public decimal cntcode { get; set; }

        public string? tenant_code { get; set; }

        public int orderno { get; set; }

        public string? shortname { get; set; }

        public string? name { get; set; }

        public string? description { get; set; }

        public int? bhcode { get; set; }

        public string? cashlcode { get; set; }

        public int? patientbilllcode { get; set; }

        public int? referabillllcode { get; set; }

        public int? patientsaleslcode { get; set; }

        public int? referalsaleslcode { get; set; }

        public int? commissionbilllcode { get; set; }

        public int? commissionsaleslcode { get; set; }

        public string? expenselcode { get; set; }

        public bool? timingcurrent { get; set; }

        public bool? timingvariable { get; set; }

        public bool? timingfixed { get; set; }

        public DateTime? timingfrom { get; set; }

        public DateTime? timingto { get; set; }

        public bool? isinsurance { get; set; }

        public int? hdcode { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }
    }
}