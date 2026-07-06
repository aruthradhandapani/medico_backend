using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("mastertenant.product_list")]
    public class ProductListModel
    {
        [ExplicitKey]
        public int id { get; set; }

        public string product_id { get; set; }

        public string product_name { get; set; }

        public string tenant_code { get; set; }

        public bool? is_active { get; set; } = true;

        public DateTime? subscription_start { get; set; }

        public DateTime? subscription_end { get; set; }

        public int? max_users { get; set; } = 0;

        public string? features { get; set; } // jsonb stored/read as string

        public DateTime? created_at { get; set; }

        public string? base_url { get; set; }

        public string? icon_css { get; set; }

        public string? connection_string { get; set; }
    }
}