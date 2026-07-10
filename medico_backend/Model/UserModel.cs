using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    // ─── Table Models ─────────────────────────────────────────────────────────

    [Table("mastertenant.user_master")]
    public class user_master
    {
        [ExplicitKey]
        public int user_code { get; set; } = 1;
        public int? ibsd_code { get; set; }
        public int? order_no { get; set; }
        public string? module { get; set; }
        public string name { get; set; }
        public string short_name { get; set; }
        public string password { get; set; }
        public string? description { get; set; }
        public bool? power_user { get; set; }
        public string? user_image { get; set; }
        public string? signature_image { get; set; }
        public bool? deleted { get; set; }
        public int? euser_code { get; set; } = 1;
        public int? computer_code { get; set; }
        public DateTime? entered_date { get; set; }
        public DateTime? ibs_date { get; set; }

        public bool? all_branch { get; set; }
        public int? bh_code { get; set; }
        public decimal? cnt_code { get; set; }
        public decimal? hd_code { get; set; }
        public decimal? op_cnt_code { get; set; }
        public decimal? ip_cnt_code { get; set; }
        public decimal? pharma_cnt_code { get; set; }
        public bool? is_consultant { get; set; }
        public int? consultant_dcode { get; set; }
        public string? tenant_code { get; set; }
        public string? global_user_id { get; set; }
        public string email { get; set; }
        public string mobile { get; set; }
        public bool? is_power_user { get; set; }
        public string? spouse_name { get; set; }
        public string? father_name { get; set; }
        public string? mother_name { get; set; }
        public string? mobile_alternate { get; set; }
        public string? permanent_address { get; set; }
        public string? current_address { get; set; }
        public string? id_document_path { get; set; }
        public DateTime? dob { get; set; }
        public string? passport_no { get; set; }
        public string? emirates_id { get; set; }
        public string? gender { get; set; }
        public string? status { get; set; }
        public int? age { get; set; }
        public string? age_type { get; set; }
        public DateTime? modified_date { get; set; }
        public bool? is_verified { get; set; }

        [Write(false)]
        public string? address { get; set; }
        public string? role { get; set; }
        public bool? edit_bill { get; set; }
        public bool? print_bill { get; set; }
        public bool? centralized_sample { get; set; }
    }

    [Table("mastertenant.user_branch_master")]
    public class UserBranchMaster
    {
        [Key]
        public long? id { get; set; }
        public int? user_code { get; set; }
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public DateTimeOffset entereddate { get; set; }
        public DateTimeOffset? ibsdate { get; set; }
        public bool deleted { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("user_group_master")]
    public class UserGroupMaster
    {
        [Key]
        public long udid { get; set; }
        public int? user_code { get; set; }
        public int? gcode { get; set; }
        public DateTimeOffset entereddate { get; set; }
        public DateTimeOffset? ibsdate { get; set; }
        public bool deleted { get; set; }
        public string? tenant_code { get; set; }
    }

    // ─── Composite request model used by the controller's multipart-form endpoints ───
    // ASP.NET Core's form model binder supports nested complex objects/lists for
    // [FromForm] using key prefixes, e.g.:
    //   Profile.User.name=John
    //   Profile.Branches[0].bhcode=1&Profile.Branches[0].cntcode=2
    //   Profile.Departments[0].dcode=5
    //
    // NOTE: tenant_code is NOT part of this model — it's a separate, explicit
    // top-level [FromForm]/[FromQuery] parameter on every controller action
    // (manual entry), instead of being read from a "tenant_code" request header.
    public class UserProfileFormModel
    {
        public user_master User { get; set; } = new();
        public IList<UserBranchMaster>? Branches { get; set; }
        public IList<UserGroupMaster>? Departments { get; set; }
    }

    public class LoginDto
    {
        public string input { get; set; }
        public string password { get; set; }
    }

    public class UserSafeDto
    {
        public int usercode { get; set; }
        public string? name { get; set; }
        public string? shortname { get; set; }
        public string? description { get; set; }
        public string? email { get; set; }
        public string? mobile { get; set; }
        public string? userimage { get; set; }
        public string? signatureimage { get; set; }
        public bool? poweruser { get; set; }
        public int? bhcode { get; set; }
        public decimal? cntcode { get; set; }
        public string? tenant_code { get; set; }
        public string? address { get; set; }
        public string? role { get; set; }
        public IList<UserBranchMaster> branches { get; set; } = new List<UserBranchMaster>();
        public IList<UserGroupMaster> departments { get; set; } = new List<UserGroupMaster>();
    }

    public class LoginResponse
    {
        public string token { get; set; } = "";
        public UserSafeDto userdetails { get; set; } = new();
    }

    public class LoginResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; } = "";
        public user_master? User { get; private set; }
        public IList<UserBranchMaster> Branches { get; set; } = new List<UserBranchMaster>();
        public IList<UserGroupMaster> Departments { get; set; } = new List<UserGroupMaster>();

        public static LoginResult Fail(string message) =>
            new() { Success = false, Message = message };

        public static LoginResult Ok(user_master user, IList<UserBranchMaster> branches, IList<UserGroupMaster> departments) =>
            new() { Success = true, Message = "OK", User = user, Branches = branches, Departments = departments };
    }

    [Table("mastertenant.product_role_templates")]
    public class product_role_templates
    {
        [Key]
        public int? template_id { get; set; }
        public string? product_id { get; set; }
        public string? role_name { get; set; }
        public string? rights_json { get; set; }
        public string? tenant_code { get; set; }
        public DateTime? created_at { get; set; }
    }

    // ─── Aggregate result for GetAllUsers ────────────────────────────────────
    public class GetUser
    {
        public user_master user { get; set; }
        public IList<UserBranchMaster> branch { get; set; } = new List<UserBranchMaster>();
        public IList<UserGroupMaster> department { get; set; } = new List<UserGroupMaster>();
    }
}