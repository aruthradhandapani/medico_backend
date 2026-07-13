using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    [Table("unbilledcharges")]
    public class UnbilledChargeRow
    {
        [ExplicitKey] public string? unbilledid { get; set; }
        public string? entrytype { get; set; }      
        public string? entryid { get; set; }         
        public DateTime? chargedate { get; set; }
        public decimal? custid { get; set; }
        public string? opvisitid { get; set; }
        public string? ipvisitid { get; set; }
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
        public int? tcode { get; set; }          // consultation charge tcode from your charge master
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
        public string? item_name { get; set; }   // joined from tests/consultation description if you have a code master
    }
    public static class IpEntryType
    {
        public const string ROOM = "IP_ROOM";
        public const string NURSING = "IP_NURSING";
        public const string INVESTIGATION = "IP_INVESTIGATION";
        public const string CONSULTATION = "IP_CONSULTATION";
        public const string PROCEDURE = "IP_PROCEDURE";
        public const string PHARMACY = "IP_PHARMACY";
    }

    // ── Request DTOs ─────────────────────────────────────────────
    public class AddIpNursingChargeRequest
    {
        public Guid ip_id { get; set; }
        public decimal custid { get; set; }
        public int tcode { get; set; }
        public double rate { get; set; }
        public double quantity { get; set; } = 1;
        public double discount { get; set; } = 0;
        public double charityamount { get; set; } = 0;
    }

    public class AddIpTestChargeRequest
    {
        public Guid ip_id { get; set; }
        public decimal custid { get; set; }
        public string entryid { get; set; } = string.Empty; // e.g. inv_det_id
        public int tcode { get; set; }
        public double rate { get; set; }
        public double quantity { get; set; } = 1;
        public double discount { get; set; } = 0;
        public double charityamount { get; set; } = 0;
    }

    public class MarkIpChargesBilledRequest
    {
        public List<string> unbilledids { get; set; } = new();
        public string billno { get; set; } = string.Empty;
        public string billid { get; set; } = string.Empty;
        public double billedquantity { get; set; }
        public double billedamount { get; set; }
    }
}