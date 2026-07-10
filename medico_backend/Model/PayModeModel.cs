using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("public.paymode_master")]
    public class PaymodeMasterModel
    {
        // pmcode is NOT a DB identity/serial column — the app generates the
        // next value per tenant (see PaymodeMasterClass.Insert), so it's
        // treated as an explicit key rather than a Dapper.Contrib [Key].
        [ExplicitKey]
        public decimal pmcode { get; set; }

        public int orderno { get; set; }

        public string? shortname { get; set; }

        public string? name { get; set; }

        public int durationtime { get; set; }

        public string? duration { get; set; }

        public string? description { get; set; }

        public string? footer { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }

        public string? tenant_code { get; set; }
    }
}