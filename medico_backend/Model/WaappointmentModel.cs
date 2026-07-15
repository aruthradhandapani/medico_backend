using System;
using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    [Table("wa_appointment_session")]
    public class WaAppointmentSession
    {
        [Key] public int sessionid { get; set; }
        public string phonenumber { get; set; } = string.Empty;
        public string currentstep { get; set; } = "WELCOME";
        public string? intent { get; set; }
        public string? patientname { get; set; }
        public int? patientage { get; set; }
        public string? patientgender { get; set; }
        public string? patientcity { get; set; }
        public int? departmentid { get; set; }
        public int? doctorid { get; set; }
        public DateTime? slotdate { get; set; }
        public string? slottime { get; set; }
        public string? appointmentidref { get; set; }
        public DateTime? createdat { get; set; }
        public DateTime? updatedat { get; set; }
        public bool? isactive { get; set; } = true;
        public string? language { get; set; }
    }

    // ── Request DTOs ──────────────────────────────────────────────

    public class CreateWaSessionRequest
    {
        public string phonenumber { get; set; } = string.Empty;
        public string? currentstep { get; set; }
        public string? intent { get; set; }
        public string? language { get; set; }
    }

    public class UpdateWaSessionRequest
    {
        public int sessionid { get; set; }
        public string? currentstep { get; set; }
        public string? intent { get; set; }
        public string? patientname { get; set; }
        public int? patientage { get; set; }
        public string? patientgender { get; set; }
        public string? patientcity { get; set; }
        public int? departmentid { get; set; }
        public int? doctorid { get; set; }
        public DateTime? slotdate { get; set; }
        public string? slottime { get; set; }
        public string? appointmentidref { get; set; }
        public string? language { get; set; }
    }

    public class CloseWaSessionRequest
    {
        public int sessionid { get; set; }
    }
    public class InsertFullWaSessionRequest
    {
        public string phonenumber { get; set; } = string.Empty;
        public string? currentstep { get; set; }
        public string? intent { get; set; }
        public string? patientname { get; set; }
        public int? patientage { get; set; }
        public string? patientgender { get; set; }
        public string? patientcity { get; set; }
        public int? departmentid { get; set; }
        public int? doctorid { get; set; }
        public DateTime? slotdate { get; set; }
        public string? slottime { get; set; }
        public string? appointmentidref { get; set; }
        public bool? isactive { get; set; }
        public string? language { get; set; }
    }

}