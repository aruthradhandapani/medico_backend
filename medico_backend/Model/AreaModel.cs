using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("area_master")]
    public class AreaMasterModel
    {
        [ExplicitKey]
        public int areacode { get; set; }

        public string? tenant_code { get; set; }

        public int orderno { get; set; }

        public string? shortname { get; set; }

        public string? areaname { get; set; }

        public int? citycode { get; set; }

        public string? areapincode { get; set; }

        public int? statecode { get; set; }

        public int? countrycode { get; set; }

        public string? description { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }
    }
}