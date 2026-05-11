using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("city_master")]
    public class CityMasterModel
    {
        [ExplicitKey]
        public int citycode { get; set; }

        public int orderno { get; set; }

        public string? shortname { get; set; }

        public string? cityname { get; set; }

        public string? description { get; set; }

        public bool deleted { get; set; } = false;

        public int usercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }
    }
}