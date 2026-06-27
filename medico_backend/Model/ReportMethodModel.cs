using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("report_method")]
    public class ReportMethodModel
    {
        [ExplicitKey]
        public decimal rtmcode { get; set; }

        public string? tenant_code { get; set; }

        public int orderno { get; set; }

        public string? shortname { get; set; }

        public string? name { get; set; }

        public int durationtime { get; set; }

        public string? duration { get; set; }

        public string? description { get; set; }

        public string? footer { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }
    }
}