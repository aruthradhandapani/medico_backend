using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("commissionpayment_master")]
    public class CommissionPaymentMasterModel
    {
        [ExplicitKey]
        public string? commissionpaymentguid { get; set; }

        public DateTime? commissionpaymentdate { get; set; }

        public int? commissionpaymentsno { get; set; }

        public string? commissionpaymentbarcode { get; set; }

        public string? commissionpaymentcovertedbarcode { get; set; }

        public decimal? pmcode { get; set; }

        public long? payledgercode { get; set; }

        public string? bankname { get; set; }

        public string? paymentreference { get; set; }

        public DateTime? chequedate { get; set; }

        public string? cardno { get; set; }

        public DateTime? carddate { get; set; }

        public double? amountpaid { get; set; }

        public double? amountadjusted { get; set; }

        public double? amounttotal { get; set; }

        public bool? deleted { get; set; } = false;

        public int? usercode { get; set; } = 1;

        public int? computercode { get; set; } = 1;

        public DateTime? entereddate { get; set; }

        public DateTime? ibsdate { get; set; }
    }
}