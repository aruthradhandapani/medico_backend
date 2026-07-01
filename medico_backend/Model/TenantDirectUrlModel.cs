using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("mastertenant.tenant_direct_url")]
    public class TenantDirectUrlModel
    {
        [ExplicitKey]
        public long id { get; set; }

        public string? tenant_code { get; set; }

        public string? tenant_name { get; set; }
         
        public string? title { get; set; }

        public string? url { get; set; }

        public DateTime entered_date { get; set; }

        public DateTime? ibsd_date { get; set; }

        public bool? isdeleted { get; set; } = false;
    }
}