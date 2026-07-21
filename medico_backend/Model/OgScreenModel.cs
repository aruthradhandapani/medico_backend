// Model/OgQueueModel.cs
using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("og_queue")]
    public class OgQueueModel
    {
        [Key]
        public int ogentryid { get; set; }

        public string? tenant_code { get; set; }

        // same token_no as vitals_entry — not generated independently anymore
        public string? og_token_no { get; set; }

        public string? custcode { get; set; }

        public int? dcode { get; set; }

        public TimeOnly? arrival_time { get; set; }

        public string entry_type { get; set; } = "direct"; // direct | test_completed

        public TimeOnly? out_time { get; set; }

        public string? notes { get; set; }

        // waiting | in_consultation | completed
        public string? status { get; set; } = "waiting";

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime created_at { get; set; }

        public DateTime updated_at { get; set; }

        public bool deleted { get; set; } = false;
    }

    public class UpdateOgOutTimeRequest
    {
        public int ogentryid { get; set; }
        public TimeOnly out_time { get; set; }
        public string? status { get; set; }
        public int usercode { get; set; } = 1;
        public int computercode { get; set; } = 1;
    }

    public class UpdateOgStatusRequest
    {
        public int ogentryid { get; set; }
        public string status { get; set; } = "";
        public int usercode { get; set; } = 1;
        public int computercode { get; set; } = 1;
    }
}