using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    // One age bracket + its OP charge for a doctor, e.g.
    // min_age=0, max_age=12, opcharge=200 (only used when
    // lab_settings.op_age_wise_split = true for the tenant)
    [Table("doctor_op_charge_slab")]
    public class OpChargeSlabModel
    {
        [Key]
        public int slabid { get; set; }

        public string? tenant_code { get; set; }

        public int dcode { get; set; }

        public int min_age { get; set; }

        public int max_age { get; set; }

        public double opcharge { get; set; }

        public bool deleted { get; set; } = false;

        public DateTime? created_at { get; set; }

        public DateTime? updated_at { get; set; }
    }
}