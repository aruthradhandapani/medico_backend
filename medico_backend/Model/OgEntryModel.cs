// Model/OgScreenModel.cs
namespace Medico_Backend.Model
{
    public class OgScreenModel
    {
        public int s_no { get; set; }
        public int id { get; set; }
        public string? token_no { get; set; }
        public string? patient_name { get; set; }
        public DateTime? in_time { get; set; }
        public DateTime? out_time { get; set; }
        public string? notes { get; set; }
        public string? status { get; set; }   // ongoing | completed
    }

    public class OgScreenStatusUpdateRequest
    {
        public int id { get; set; }
        public string status { get; set; } = "";   // ongoing | completed
        public string? notes { get; set; }
        public int usercode { get; set; } = 1;
        public int computercode { get; set; } = 1;
    }
}