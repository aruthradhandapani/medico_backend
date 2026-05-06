using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    [Table("prescription_master")]
    public class PrescriptionModel
    {
        public int id { get; set; }
        public string filename { get; set; } = string.Empty;
        public string bucketname { get; set; } = string.Empty;
        public string filepath { get; set; } = string.Empty;
        public DateTime uploaded_date { get; set; }
    }
}