using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("machine_master")]
    public class MachineMasterModel
    {
        [ExplicitKey]
        public int mccode { get; set; }
        public int orderno { get; set; }
        public string? shortname { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public string? manufacturer { get; set; }
        public string? model { get; set; }
        public string? portnumber { get; set; }
        public double? baudrate { get; set; }
        public string? parity { get; set; }
        public int? stopbits { get; set; }
        public int? databits { get; set; }
        public bool deleted { get; set; } = false;
        public int usercode { get; set; } = 1;
        public int computercode { get; set; } = 1;
        public DateTime entereddate { get; set; }
        public DateTime ibsdate { get; set; }
        public bool? opttcp { get; set; }
        public bool? optlmport { get; set; }
        public string? inputpath { get; set; }
        public string? outputpath { get; set; }
        public bool? optrs232 { get; set; }
        public bool? opttcpclient { get; set; }
        public bool? opttcpserver { get; set; }
        public string? tenant_code { get; set; }
    }
}