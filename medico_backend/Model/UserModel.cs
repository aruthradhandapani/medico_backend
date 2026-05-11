using Dapper.Contrib.Extensions;

namespace Medico_Backend.Model
{
    [Table("user_master")]
    public class UserMasterModel
    {
        [ExplicitKey]
        public int usercode { get; set; }

        public int ibsdcode { get; set; }

        public int orderno { get; set; }

        public string? Module { get; set; }

        public string? name { get; set; }

        public string? shortname { get; set; }

        public string? Password { get; set; }

        public string? description { get; set; }

        public bool poweruser { get; set; } = false;

        public byte[]? userimage { get; set; }

        public byte[]? signatureimage { get; set; }

        public bool deleted { get; set; } = false;

        public int eusercode { get; set; } = 1;

        public int computercode { get; set; } = 1;

        public DateTime entereddate { get; set; }

        public DateTime ibsdate { get; set; }

        public decimal? bhcode { get; set; }

        public decimal? cntcode { get; set; }

        public decimal? hdcode { get; set; }

        public decimal? opcntcode { get; set; }

        public decimal? ipcntcode { get; set; }

        public decimal? pharmacntcode { get; set; }

        public bool? isconsultant { get; set; }

        public int? consultantdcode { get; set; }

        public string? tenant_code { get; set; }
    }
    public class LoginModel
    {
        public string? shortname { get; set; }

        public string? Password { get; set; }
    }
    public class ResetPasswordModel
    {
        public string? shortname { get; set; }

        public string? oldpassword { get; set; }

        public string? newpassword { get; set; }
    }
}