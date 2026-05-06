using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("customer_master")]
    public class CustomerMasterModel
    {
        [ExplicitKey]
        public decimal custid { get; set; }

        public string? custcode { get; set; }

        public string? bhcustcode { get; set; }

        public int? dcode { get; set; }

        public int bhcode { get; set; }

        public DateTime? ageedate { get; set; }

        public DateTime? dateofbirth { get; set; }

        public string? dobinstring { get; set; }

        public int ageyears { get; set; }

        public int agemonths { get; set; }

        public int agedays { get; set; }

        public string? gender { get; set; }

        public string? nametitile { get; set; }

        public string? initial { get; set; }

        public string? name { get; set; }

        public string? careoftitile { get; set; }

        public string? careofinitial { get; set; }

        public string? careof { get; set; }

        public string? street { get; set; }

        public string? area { get; set; }

        public string? city { get; set; }

        public string? zipcode { get; set; }

        public string? state { get; set; }

        public string? country { get; set; }

        public string? phonestdcode { get; set; }

        public string? phone { get; set; }

        public string? faxstdcode { get; set; }

        public string? fax { get; set; }

        public string? mobile { get; set; }

        public string? email { get; set; }

        public string? description { get; set; }

        public int? areacode { get; set; }

        public string? namewithinitials { get; set; }

        public string? customerimage { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }

        public bool? isdiabetic { get; set; }

        public bool? isregular { get; set; }

        public string? customerbarcode { get; set; }

        public int? ptcode { get; set; }

        public int? ocpcode { get; set; }

        public int? rlgcode { get; set; }

        public int? mscode { get; set; }

        public DateTime? regdate { get; set; }

        public int? ftcode { get; set; }

        public int? bgcode { get; set; }

        public string? emergencycontactno { get; set; }

        // ✅ Changed from string to Guid (table is UUID now)
        public Guid? lastopvisitid { get; set; }

        public bool? lastopstatus { get; set; }

        public DateTime? consultingchargevalidity { get; set; }

        public int? consultingvisitvalidity { get; set; }

        public string? customermanualcode { get; set; }

        public int? hdcode { get; set; }

        public bool? isip { get; set; }

        public bool? isop { get; set; }

        public int? consultantdcode { get; set; }

        public int? ricode { get; set; }

        public string? insuranceauthorizationo { get; set; }

        public bool? isinsurancepatient { get; set; }

        public string? policyno { get; set; }

        public double? insurancepayamount { get; set; }

        public string? proof { get; set; }

        public string? prooftype { get; set; }

        public string? annualincome { get; set; }

        // ✅ Changed from string to Guid (table is UUID now)
        public Guid? insurancelastopvisitid { get; set; }

        public bool? insurancelastopstatus { get; set; }

        public bool? insuranceisip { get; set; }

        public bool? insuranceisop { get; set; }

        public string? gstno { get; set; }

        public string? fileno { get; set; }

        public int attage { get; set; }

        // ✅ New columns added
        public double? longitude { get; set; }

        public double? latitude { get; set; }

        public string? occupation { get; set; }

        public string? tenant_code { get; set; }
    }
}