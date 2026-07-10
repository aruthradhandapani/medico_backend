using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    [Table("public.bed_status")]
    public class BedStatusModel
    {
        [ExplicitKey]
        public Guid bed_status_id { get; set; } = Guid.NewGuid();

        public int bedcode { get; set; }
        public Guid? ip_id { get; set; }
        public string? ip_no { get; set; }
        public decimal? custid { get; set; }

        public string status { get; set; } = "OCCUPIED";
        public DateTime? admitted_at { get; set; }
        public DateTime? discharged_at { get; set; }

        public bool is_cleaned { get; set; } = false;
        public DateTime? cleaned_at { get; set; }
        public string? cleaned_by { get; set; }

        public string? notes { get; set; }
        public string? tenant_code { get; set; }
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime updated_at { get; set; } = DateTime.UtcNow;
    }

    public class MarkBedCleanedRequest
    {
        public int bedcode { get; set; }
        public string? cleaned_by { get; set; }
        public string? notes { get; set; }
    }
}