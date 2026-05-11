using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("test_fee_master")]
    public class TestFeeMasterModel
    {
        [ExplicitKey]
        public decimal tfcode { get; set; }

        public decimal? tcode { get; set; }

        public decimal? ftcode { get; set; }

        public string? margintype { get; set; }

        public double? marginpercentage { get; set; }

        public double? marginamount { get; set; }

        public double amount { get; set; }

        public double? feeper { get; set; }

        public double? feeamount { get; set; }

        public double? charityper { get; set; }

        public double? charityamount { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }

        public double? runningcost { get; set; }

        public string? commissiontype { get; set; }

        // Tenant
        public string? tenant_code { get; set; }
    }
}