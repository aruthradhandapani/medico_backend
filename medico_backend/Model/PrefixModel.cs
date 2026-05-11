using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("prefix_master")]
    public class PrefixMasterModel
    {
        [ExplicitKey]
        public int prefixcode { get; set; }

        public string? prefixname { get; set; }

        public bool deleted { get; set; }

        public int usercode { get; set; }

        public int computercode { get; set; }

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }
    }
}