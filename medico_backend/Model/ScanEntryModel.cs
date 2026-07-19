// Model/ScanResultEntryModel.cs
namespace Medico_Backend.Model
{
    public class ScanResultEntryModel
    {
        public int id { get; set; }
        public string? token_no { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobile { get; set; }
        public string? test_name { get; set; }   // scan test, e.g. Ultrasound, X-Ray
        public string? status { get; set; }
        public DateTime entered_date { get; set; }
    }

    public class ScanStatusUpdateRequest
    {
        public int id { get; set; }
        public string status { get; set; } = "";
        public int usercode { get; set; } = 1;
        public int computercode { get; set; } = 1;
    }
}