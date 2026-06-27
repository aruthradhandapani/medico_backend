using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("commissionpayment_details")]
    public class CommissionPaymentDetailsModel
    {
        public string? commissionpaymentguid { get; set; }

        public string? requestguid { get; set; }

        public double? commissionpaymentamount { get; set; }

        [ExplicitKey]
        public string? cpdid { get; set; }
    }
}