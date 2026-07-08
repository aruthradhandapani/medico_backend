using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("public.bed_transfer")]
    public class BedTransferModel
    {
        [Key]
        public int transferid { get; set; }

        public string? lastvisitid { get; set; }

        public decimal? custid { get; set; }

        public string? custcode { get; set; }

        public DateTime? admitteddate { get; set; }

        public int? currentfloor { get; set; }

        public int? currentroom { get; set; }

        public int? currentbed { get; set; }

        public DateTime? transferdate { get; set; }

        public int? transfloor { get; set; }

        public int? transroom { get; set; }

        public int? transbed { get; set; }

        public string? transferedby { get; set; }

        public string? reason { get; set; }

        public bool? ischeckout { get; set; }

        public int? usercode { get; set; } = 1;

        public int? computercode { get; set; } = 1;

        public DateTime? entereddate { get; set; }

        public string? tenant_code { get; set; }
    }
}