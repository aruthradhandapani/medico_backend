
namespace Medico_Backend.Model
{
    
    public class LabResultEntryModel
    {
        public int vitalentryid { get; set; }
        public string? token_no { get; set; }
        public string? custcode { get; set; }
        public string? patient_name { get; set; }
        public string? mobile { get; set; }
        public string? status { get; set; }
        public DateTime entered_date { get; set; }
        public DateTime updated_at { get; set; }
    }

    public class LabStatusUpdateRequest
    {
        public int vitalentryid { get; set; }
        public string status { get; set; } = "";   // waiting_for_test | on_going | completed | result_pending | report_received
        public int usercode { get; set; } = 1;
        public int computercode { get; set; } = 1;
    }
}