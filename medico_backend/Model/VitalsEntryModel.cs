using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("vitals_entry")]
    public class VitalsModel
    {
        [Key]
        public int vitalentryid { get; set; }

        public string? tenant_code { get; set; }

        public string? token_no { get; set; }

        public string? custcode { get; set; }

        public int? dcode { get; set; }

        // routes the token: lab | scan | doctor
        public string? investigation { get; set; }

        // the specific test/scan name, e.g. "CBC", "Ultrasound"
        public string? test_name { get; set; }

        // waiting_for_test | on_going | completed | result_pending | report_received
        public string? status { get; set; } = "waiting_for_test";

        public DateTime entered_date { get; set; }

        public TimeOnly? arrival_time { get; set; }

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime created_at { get; set; }

        public DateTime updated_at { get; set; }

        public bool deleted { get; set; } = false;
    }
}