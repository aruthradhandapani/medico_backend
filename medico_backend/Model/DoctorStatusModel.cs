using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("doctor_current_status")]
    public class DoctorCurrentStatusModel
    {
        [Key]
        public long status_id { get; set; }

        public long dcode { get; set; }

        public string? tenant_code { get; set; }

        public string status { get; set; } = "OFF_DUTY";

        public string? remarks { get; set; }

        public DateTime? expected_return_time { get; set; }

        public bool is_available_for_booking { get; set; } = true;

        public bool is_queue_paused { get; set; } = false;

        public long? updated_by { get; set; }

        public DateTime created_at { get; set; }

        public DateTime updated_at { get; set; }
    }
}