// Model/LabResultEntryModel.cs
namespace Medico_Backend.Model
{
    // Display row for the Lab Result Entry grid
    public class LabResultEntryModel
    {
        public int id { get; set; }
        public string? token_no { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobile { get; set; }
        public string? test_name { get; set; }
        public string? status { get; set; }
        public DateTime entered_date { get; set; }
    }

    public class LabStatusUpdateRequest
    {
        public int id { get; set; }
        public string status { get; set; } = "";   // waiting_for_test | on_going | completed | result_pending | report_received
        public int usercode { get; set; } = 1;
        public int computercode { get; set; } = 1;
    }
}