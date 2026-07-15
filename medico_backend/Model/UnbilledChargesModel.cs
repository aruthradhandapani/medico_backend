using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    [Table("unbilledcharges")]
    public class UnbilledChargeRow
    {
        [ExplicitKey] public string? unbilledid { get; set; }
        public string? entrytype { get; set; }      // "CONSULTATION" | "INVESTIGATION"
        public string? entryid { get; set; }         // op_id (consultation) or inv_det_id (investigation)
        public DateTime? chargedate { get; set; }
        public decimal? custid { get; set; }
        public string? opvisitid { get; set; }        // op_id
        public int? tcode { get; set; }
        public double? quantity { get; set; }
        public double? rate { get; set; }
        public double? amount { get; set; }
        public double? discount { get; set; }
        public double? charityamount { get; set; }
        public bool? billedstatus { get; set; }
        public string? billno { get; set; }
        public string? billid { get; set; }
        public DateTime? billeddate { get; set; }
        public double? billedquantity { get; set; }
        public double? billedamount { get; set; }
        public string? tenant_code { get; set; }      
    }

    // ── Request DTOs ─────────────────────────────────────────────
    public class AddUnbilledConsultationRequest
    {
        public string op_id { get; set; } = string.Empty;
        public decimal custid { get; set; }
        public int? tcode { get; set; }          
        public double rate { get; set; }
        public double amount { get; set; }
        public double quantity { get; set; } = 1;
    }

    public class UnbilledChargeSummary
    {
        public string? unbilledid { get; set; }
        public string? entrytype { get; set; }
        public string? entryid { get; set; }
        public DateTime? chargedate { get; set; }
        public int? tcode { get; set; }
        public double? quantity { get; set; }
        public double? rate { get; set; }
        public double? amount { get; set; }
        public string? item_name { get; set; }   
    }
}