using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("doctor_master")]
    public class DoctorMasterModel
    {
        [Key]
        public int dcode { get; set; }

        public int? orderno { get; set; }

        public string? description { get; set; }

        public string? name { get; set; }

        public string? qualification { get; set; }

        public string? shortname { get; set; }

        public int? spcode { get; set; }

        public int? tcode { get; set; }

        public int? ftcode { get; set; }

        public int? ricode { get; set; }

        public int? procode { get; set; }

        public string? reference { get; set; }

        public string? address { get; set; }

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

        public string? website { get; set; }

        public bool? appdoctor { get; set; }

        public int? areacode { get; set; }

        public string? importreference { get; set; }

        public bool deleted { get; set; } = false;

        public int? usercode { get; set; } = 1;

        public int? computercode { get; set; } = 1;

        public DateTime? entereddate { get; set; }

        public DateTime? ibsdate { get; set; }

        public string? initial { get; set; }

        public string? nametitle { get; set; }

        public string? doctorcode { get; set; }

        public string? onlinepassword { get; set; }

        public string? entrytype { get; set; }

        public bool? sendaccounts { get; set; }

        public bool? sendresults { get; set; }

        public bool? sendoffer { get; set; }

        public bool? sendbillinfo { get; set; }

        public string? accountsmobileno { get; set; }

        public string? resultmobileno { get; set; }

        public string? contactperson { get; set; }

        public string? smslabname { get; set; }

        public string? bedno { get; set; }

        public string? doctorfullname { get; set; }

        public bool? isreferral { get; set; }

        public bool? isconsultant { get; set; }

        public bool? setdefaultreferal { get; set; }

        public double? opcharge { get; set; }

        public string? tenant_code { get; set; }
        public string? doctorimage { get; set; }
        public bool showmobile { get; set; } = true;
    }
}