using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("mastertenant.tenant_product_subscription")]
    public class TenantProductSubscriptionModel
    {
        [Key]
        public int id { get; set; }

        public string? tenant_code { get; set; }

        public string? product_id { get; set; }

        public decimal amount_paid { get; set; }

        public string? currency { get; set; } = "INR";

        public string? payment_mode { get; set; }

        public string? transaction_id { get; set; }

        public string? invoice_number { get; set; }

        public string? payment_status { get; set; } = "pending";

        public string? billing_cycle { get; set; } = "one_time";

        public DateTime start_date { get; set; }

        public DateTime? end_date { get; set; }

        public string? status { get; set; } = "active";

        public int? max_users { get; set; } = 0;

        public string? purchased_by { get; set; }

        public string? remarks { get; set; }

        public DateTime created_at { get; set; }

        public DateTime updated_at { get; set; }
    }
}