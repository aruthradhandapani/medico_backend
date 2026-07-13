using BCrypt.Net;
using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public class UserClass
    {
        private readonly string _dbConn;

        public UserClass(IConfiguration configuration)
        {
            _dbConn = configuration.GetConnectionString("conn");
        }

        private IDbConnection Connection() => new NpgsqlConnection(_dbConn);

        // ─── View Profile ─────────────────────────────────────────────────────

        public async Task<user_master?> GetUserByEmail(string email, string tenant_code)
        {
            using IDbConnection db = Connection();
            return await db.QueryFirstOrDefaultAsync<user_master>(@"
                SELECT user_code, user_image, signature_image
                FROM mastertenant.user_master
                WHERE email       = @email
                  AND tenant_code = @tenant_code
                  AND deleted     = false
                LIMIT 1",
                new { email, tenant_code });
        }

        public async Task<user_master?> View_profile(int user_code, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();
                return await db.QueryFirstOrDefaultAsync<user_master>(@"
                    SELECT * FROM mastertenant.user_master
                    WHERE user_code   = @user_code
                      AND tenant_code = @tenant_code
                      AND deleted     = false",
                    new { user_code, tenant_code });
            }
            catch (Exception ex)
            {
                throw new Exception($"View_profile failed: {ex.Message}");
            }
        }

        // ─── Update Images Only ───────────────────────────────────────────────

        public async Task UpdateImages(
            int user_code,
            string tenant_code,
            string? userImageKey,
            string? signatureImageKey)
        {
            if (userImageKey == null && signatureImageKey == null)
                return;

            using IDbConnection db = Connection();

            var user = await db.QueryFirstOrDefaultAsync<user_master>(@"
                SELECT * FROM mastertenant.user_master
                WHERE user_code   = @user_code
                  AND tenant_code = @tenant_code
                  AND deleted     = false",
                new { user_code, tenant_code });

            if (user == null) return;

            if (userImageKey != null) user.user_image = userImageKey;
            if (signatureImageKey != null) user.signature_image = signatureImageKey;
            user.modified_date = DateTime.UtcNow;

            await db.UpdateAsync(user);  // ← Dapper.Contrib
        }

        // ─── Bulk child-table inserts ───────────────────────────────────────────
        // Both helpers reserve a contiguous ID block with ONE round trip, then
        // insert ALL rows with ONE statement via UNNEST — avoids N round trips
        // for N branches/departments.

        private static async Task InsertBranches(
            IDbConnection db,
            int userCode,
            string tenantCode,
            IList<UserBranchMaster> branches,
            DateTimeOffset enteredAt,
            IDbTransaction? tx = null)
        {
            if (branches.Count == 0) return;

            var maxId = await db.ExecuteScalarAsync<long>(
                "SELECT COALESCE(MAX(id), 0) FROM mastertenant.user_branch_master",
                null, tx);

            long nextId = maxId;
            var ids = new long[branches.Count];
            var bhcodes = new int?[branches.Count];
            var cntcodes = new int?[branches.Count];

            for (int i = 0; i < branches.Count; i++)
            {
                ids[i] = ++nextId;
                bhcodes[i] = branches[i].bhcode;
                cntcodes[i] = branches[i].cntcode;
            }

            await db.ExecuteAsync(@"
                INSERT INTO mastertenant.user_branch_master
                    (id, user_code, tenant_code, bhcode, cntcode, entereddate, ibsdate, deleted)
                SELECT u, @UserCode, @TenantCode, bh, ct, @EnteredAt, @EnteredAt, false
                FROM UNNEST(@Ids, @BhCodes, @CntCodes) AS t(u, bh, ct)",
                new
                {
                    Ids = ids,
                    BhCodes = bhcodes,
                    CntCodes = cntcodes,
                    UserCode = userCode,
                    TenantCode = tenantCode,
                    EnteredAt = enteredAt
                },
                tx);
        }

        private static async Task InsertDepartments(
            IDbConnection db,
            int userCode,
            string tenantCode,
            IList<UserGroupMaster> departments,
            DateTimeOffset enteredAt,
            IDbTransaction? tx = null)
        {
            if (departments.Count == 0) return;

            var maxId = await db.ExecuteScalarAsync<long>(
                "SELECT COALESCE(MAX(udid), 0) FROM user_group_master",
                null, tx);

            long nextId = maxId;
            var ids = new long[departments.Count];
            var gcodes = new int?[departments.Count];

            for (int i = 0; i < departments.Count; i++)
            {
                ids[i] = ++nextId;
                gcodes[i] = departments[i].gcode;
            }

            await db.ExecuteAsync(@"
                INSERT INTO user_group_master
                    (udid, user_code, gcode, entereddate, ibsdate, deleted, tenant_code)
                SELECT u, @UserCode, d, @EnteredAt, @EnteredAt, false, @TenantCode
                FROM UNNEST(@Ids, @GCodes) AS t(u, d)",
                new
                {
                    Ids = ids,
                    GCodes = gcodes,
                    UserCode = userCode,
                    EnteredAt = enteredAt,
                    TenantCode = tenantCode
                },
                tx);
        }

        // ─── Update Profile ───────────────────────────────────────────────────

        public async Task<string> Update_profile(
            user_master user,
            IList<UserBranchMaster>? branchModel,
            IList<UserGroupMaster>? deptModel)
        {
            try
            {
                using IDbConnection db = Connection();
                db.Open(); // required before BeginTransaction()

                var oldUser = await db.QueryFirstOrDefaultAsync<user_master>(@"
                    SELECT * FROM mastertenant.user_master
                    WHERE user_code   = @user_code
                      AND tenant_code = @tenant_code",
                    new { user.user_code, user.tenant_code });

                if (oldUser == null) return "User not found";

                // ── Password: hash only when actually changed ─────────────────
                if (!string.IsNullOrWhiteSpace(user.password))
                {
                    bool hashed = oldUser.password?.StartsWith("$2") == true;
                    bool changed = hashed
                        ? !BCrypt.Net.BCrypt.Verify(user.password, oldUser.password)
                        : user.password != oldUser.password;

                    user.password = changed
                        ? BCrypt.Net.BCrypt.HashPassword(user.password)
                        : oldUser.password;
                }
                else
                {
                    user.password = oldUser.password;
                }

                // ── Immutable / audit fields ──────────────────────────────────
                user.entered_date = oldUser.entered_date;
                user.ibs_date = DateTime.UtcNow;
                user.modified_date = DateTime.UtcNow;
                user.deleted = oldUser.deleted;
                user.power_user = oldUser.power_user;
                user.is_power_user = oldUser.power_user;
                user.tenant_code = oldUser.tenant_code;

                // ── Preserve nullable fields not provided in the request ──────
                user.ibsd_code ??= oldUser.ibsd_code;
                user.order_no ??= oldUser.order_no;
                user.computer_code ??= oldUser.computer_code;
                user.euser_code ??= oldUser.euser_code;
                user.bh_code ??= oldUser.bh_code;
                user.cnt_code ??= oldUser.cnt_code;
                user.hd_code ??= oldUser.hd_code;
                user.op_cnt_code ??= oldUser.op_cnt_code;
                user.ip_cnt_code ??= oldUser.ip_cnt_code;
                user.pharma_cnt_code ??= oldUser.pharma_cnt_code;
                user.module ??= oldUser.module;
                user.global_user_id ??= oldUser.global_user_id;
                user.status ??= oldUser.status;
                user.gender ??= oldUser.gender;
                user.dob ??= oldUser.dob;
                user.age ??= oldUser.age;
                user.age_type ??= oldUser.age_type;
                user.is_consultant ??= oldUser.is_consultant;
                user.consultant_dcode ??= oldUser.consultant_dcode;
                user.spouse_name ??= oldUser.spouse_name;
                user.father_name ??= oldUser.father_name;
                user.mother_name ??= oldUser.mother_name;
                user.mobile_alternate ??= oldUser.mobile_alternate;
                user.permanent_address ??= oldUser.permanent_address;
                user.current_address ??= oldUser.current_address;
                user.id_document_path ??= oldUser.id_document_path;
                user.passport_no ??= oldUser.passport_no;
                user.emirates_id ??= oldUser.emirates_id;
                user.is_power_user ??= oldUser.power_user;
                user.user_image ??= oldUser.user_image;
                user.signature_image ??= oldUser.signature_image;
                user.role ??= oldUser.role;
                user.edit_bill ??= oldUser.edit_bill;
                user.print_bill ??= oldUser.print_bill;
                user.centralized_sample ??= oldUser.centralized_sample;

                await db.UpdateAsync(user);  // ← Dapper.Contrib

                // ── Branch + Department diff (computed before opening a transaction) ──
                List<UserBranchMaster> branchToRemove = new(), branchToAdd = new();
                List<UserGroupMaster> deptToRemove = new(), deptToAdd = new();

                if (branchModel != null)
                {
                    var oldBranches = (await db.QueryAsync<UserBranchMaster>(@"
                        SELECT id, bhcode, cntcode, entereddate
                        FROM mastertenant.user_branch_master
                        WHERE user_code   = @UserCode
                          AND tenant_code = @TenantCode
                          AND deleted     = false",
                        new { UserCode = user.user_code, TenantCode = user.tenant_code }))
                        .ToList();

                    branchToRemove = oldBranches
                        .Where(ob => !branchModel.Any(nb =>
                            nb.bhcode == ob.bhcode && nb.cntcode == ob.cntcode))
                        .ToList();

                    branchToAdd = branchModel
                        .Where(nb => !oldBranches.Any(ob =>
                            ob.bhcode == nb.bhcode && ob.cntcode == nb.cntcode))
                        .ToList();
                }

                if (deptModel != null)
                {
                    var oldDepts = (await db.QueryAsync<UserGroupMaster>(@"
                        SELECT udid, gcode, entereddate
                        FROM user_group_master
                        WHERE user_code   = @UserCode
                          AND tenant_code = @TenantCode
                          AND deleted     = false",
                        new { UserCode = user.user_code, TenantCode = user.tenant_code }))
                        .ToList();

                    deptToRemove = oldDepts
                        .Where(od => !deptModel.Any(nd => nd.gcode == od.gcode))
                        .ToList();

                    deptToAdd = deptModel
                        .Where(nd => !oldDepts.Any(od => od.gcode == nd.gcode))
                        .ToList();
                }

                // ── Apply diffs in ONE shared transaction (branches + departments) ──
                if (branchToRemove.Count > 0 || branchToAdd.Count > 0 ||
                    deptToRemove.Count > 0 || deptToAdd.Count > 0)
                {
                    var now = DateTimeOffset.UtcNow;

                    using var tx = db.BeginTransaction();
                    try
                    {
                        if (branchToRemove.Count > 0)
                            await db.ExecuteAsync(@"
                                DELETE FROM mastertenant.user_branch_master
                                WHERE id = ANY(@ids)",
                                new { ids = branchToRemove.Select(b => b.id).ToArray() }, tx);

                        if (branchToAdd.Count > 0)
                            await InsertBranches(db, user.user_code, user.tenant_code!, branchToAdd, now, tx);

                        if (deptToRemove.Count > 0)
                            await db.ExecuteAsync(@"
                                DELETE FROM user_group_master
                                WHERE udid = ANY(@ids)",
                                new { ids = deptToRemove.Select(d => d.udid).ToArray() }, tx);

                        if (deptToAdd.Count > 0)
                            await InsertDepartments(db, user.user_code, user.tenant_code!, deptToAdd, now, tx);

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                return "success";
            }
            catch (Exception ex)
            {
                throw new Exception($"Update_profile failed: {ex.Message}");
            }
        }

        // ─── Soft Delete ──────────────────────────────────────────────────────

        public async Task<string> Delete_profile(int user_code, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();

                var oldUser = await db.QueryFirstOrDefaultAsync<user_master>(@"
                    SELECT * FROM mastertenant.user_master
                    WHERE user_code   = @user_code
                      AND tenant_code = @tenant_code
                      AND deleted     = false",
                    new { user_code, tenant_code });

                if (oldUser == null) return "User not found";

                var now = DateTimeOffset.UtcNow;

                oldUser.deleted = true;
                oldUser.ibs_date = now.UtcDateTime;
                await db.UpdateAsync(oldUser);  // ← Dapper.Contrib

                await db.ExecuteAsync(@"
                    UPDATE mastertenant.user_branch_master
                    SET    deleted = true,
                           ibsdate = @now
                    WHERE  user_code   = @user_code
                      AND  tenant_code = @tenant_code",
                    new { user_code, tenant_code, now });

                await db.ExecuteAsync(@"
                    UPDATE user_group_master
                    SET    deleted = true,
                           ibsdate = @now
                    WHERE  user_code   = @user_code
                      AND  tenant_code = @tenant_code",
                    new { user_code, tenant_code, now });

                return "success";
            }
            catch (Exception ex)
            {
                throw new Exception($"Delete_profile failed: {ex.Message}");
            }
        }

        // ─── Permanent Delete ─────────────────────────────────────────────────

        public async Task<string> PermanentDelete(int user_code, string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();

                var user = await db.QueryFirstOrDefaultAsync<user_master>(@"
                    SELECT * FROM mastertenant.user_master
                    WHERE user_code   = @user_code
                      AND tenant_code = @tenant_code",
                    new { user_code, tenant_code });

                if (user == null) return "User not found";

                await db.ExecuteAsync(@"
                    DELETE FROM mastertenant.user_branch_master
                    WHERE user_code   = @user_code
                      AND tenant_code = @tenant_code",
                    new { user_code, tenant_code });

                await db.ExecuteAsync(@"
                    DELETE FROM user_group_master
                    WHERE user_code   = @user_code
                      AND tenant_code = @tenant_code",
                    new { user_code, tenant_code });

                await db.DeleteAsync(user);  // ← Dapper.Contrib

                return "success";
            }
            catch (Exception ex)
            {
                throw new Exception($"PermanentDelete failed: {ex.Message}");
            }
        }

        // ─── Create / Insert ──────────────────────────────────────────────────

        public async Task<string> CreateUser(
            user_master user,
            IList<UserBranchMaster>? branchModel,
            IList<UserGroupMaster>? deptModel) =>
            await InsertUserInternal(user, branchModel, deptModel, isVerified: false);

        public async Task<string> InsertUser(
            user_master user,
            IList<UserBranchMaster>? branchModel,
            IList<UserGroupMaster>? deptModel) =>
            await InsertUserInternal(user, branchModel, deptModel, isVerified: true);

        private async Task<string> InsertUserInternal(
    user_master user,
    IList<UserBranchMaster>? branchModel,
    IList<UserGroupMaster>? deptModel,
    bool isVerified)
        {
            try
            {
                using var db = Connection();
                db.Open();

                // Trim defensively here too, in case this method is ever called
                // from somewhere other than the controller.
                user.tenant_code = user.tenant_code?.Trim();
                user.name = user.name?.Trim();
                user.email = user.email?.Trim();
                user.mobile = user.mobile?.Trim();

                // ── Step 1: Tenant check, split out from uniqueness checks ────────
                var tenantStatus = await db.QueryFirstOrDefaultAsync<bool?>(@"
            SELECT is_active
            FROM mastertenant.tenants
            WHERE tenant_code = @tenant_code",
                    new { user.tenant_code });

                if (tenantStatus == null)
                    return "Invalid tenant code"; // no row at all — tenant_code doesn't exist

                if (tenantStatus != true)
                    return "Tenant is not active"; // row exists but is_active is false or null

                // ── Step 2: Uniqueness checks ──────────────────────────────────────
                var status = await db.ExecuteScalarAsync<string>(@"
            SELECT CASE
                WHEN EXISTS (
                    SELECT 1 FROM mastertenant.user_master
                    WHERE name = @name AND deleted = false
                ) THEN 'NAME_EXISTS'
                WHEN @email IS NOT NULL AND EXISTS (
                    SELECT 1 FROM mastertenant.user_master
                    WHERE email = @email AND deleted = false
                ) THEN 'EMAIL_EXISTS'
                WHEN @mobile IS NOT NULL AND EXISTS (
                    SELECT 1 FROM mastertenant.user_master
                    WHERE mobile = @mobile AND deleted = false
                ) THEN 'MOBILE_EXISTS'
                ELSE 'OK'
            END", user);

                if (status == "NAME_EXISTS") return "Username already exists";
                if (status == "EMAIL_EXISTS") return "Email already in use";
                if (status == "MOBILE_EXISTS") return "Mobile already in use";

                string? modules = await db.QueryFirstOrDefaultAsync<string>(@"
            SELECT module
            FROM mastertenant.user_master
            WHERE tenant_code = @tenant_code
              AND power_user = true;",
                    new { user.tenant_code });

                user.module = modules ?? user.module;
                user.password = BCrypt.Net.BCrypt.HashPassword(user.password);
                var now = DateTimeOffset.UtcNow;
                user.deleted = false;
                user.entered_date = now.UtcDateTime;
                user.ibs_date = now.UtcDateTime;
                user.modified_date = now.UtcDateTime;
                user.ibsd_code ??= 1;
                user.order_no ??= 1;
                user.computer_code ??= 1;
                user.euser_code ??= 1;
                user.power_user ??= false;
                user.is_power_user ??= user.power_user;
                user.edit_bill ??= false;
                user.print_bill ??= false;
                user.centralized_sample ??= false;

                using var tx = db.BeginTransaction();
                try
                {
                    var maxCode = await db.ExecuteScalarAsync<int>(
                        "SELECT COALESCE(MAX(user_code), 0) FROM mastertenant.user_master",
                        null, tx);
                    user.user_code = maxCode + 1;

                    await db.InsertAsync(user, tx);

                    if (branchModel?.Count > 0)
                        await InsertBranches(db, user.user_code, user.tenant_code!, branchModel, now, tx);

                    if (deptModel?.Count > 0)
                        await InsertDepartments(db, user.user_code, user.tenant_code!, deptModel, now, tx);

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }

                return "User Created";
            }
            catch (Exception ex)
            {
                throw new Exception($"InsertUserInternal failed: {ex.Message}");
            }
        }
        // ─── Login (no rights lookup) ─────────────────────────────────────────

        public async Task<LoginResult> LoginWithRights(LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.password))
                return LoginResult.Fail("Invalid credentials");

            if (string.IsNullOrWhiteSpace(dto.input))
                return LoginResult.Fail("Username, Email or Mobile is required");

            using IDbConnection db = Connection();

            string column = dto.input.Contains('@') ? "email"
                          : dto.input.All(char.IsDigit) ? "mobile"
                          : "name";

            var user = await db.QueryFirstOrDefaultAsync<user_master>($@"
                SELECT * FROM mastertenant.user_master
                WHERE {column} = @input
                  AND deleted   = false
                LIMIT 1",
                new { dto.input });

            if (user == null) return LoginResult.Fail("Invalid credentials");

            if ((user.is_verified == false || user.is_verified == null) && (user.power_user == false || user.power_user == null))
                return LoginResult.Fail("You are not verified by Admin");

            bool hashed = user.password?.StartsWith("$2") == true;
            bool isValid = hashed
                ? BCrypt.Net.BCrypt.Verify(dto.password, user.password)
                : user.password == dto.password;

            if (!isValid) return LoginResult.Fail("Incorrect Password");

            bool isProductHave = user.module?.Contains("LABCARE", StringComparison.OrdinalIgnoreCase) == true;
            bool isProductHave1 = user.module?.Contains("MEDICO", StringComparison.OrdinalIgnoreCase) == true;

            if (!isProductHave && !isProductHave1) return LoginResult.Fail("Access denied. Your account is not assigned to the MEDICO module.");

            var branches = (await db.QueryAsync<UserBranchMaster>(@"
                SELECT *
                FROM mastertenant.user_branch_master
                WHERE user_code   = @user_code
                  AND tenant_code = @tenant_code
                  AND deleted     = false",
                new { user_code = user.user_code, tenant_code = user.tenant_code }))
                .ToList();

            var departments = (await db.QueryAsync<UserGroupMaster>(@"
                SELECT *
                FROM user_group_master
                WHERE user_code   = @user_code
                  AND tenant_code = @tenant_code
                  AND deleted     = false",
                new { user_code = user.user_code, tenant_code = user.tenant_code }))
                .ToList();

            return LoginResult.Ok(user, branches, departments);
        }

        // ─── Get All Users ────────────────────────────────────────────────────
        // 3 single-pass queries instead of one wide JOIN — avoids the
        // branches × departments cartesian product you'd get from a single
        // multi-mapped join, and each query stays a flat O(n) scan.

        public async Task<List<GetUser>> GetAllUsers(string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();

                var users = (await db.QueryAsync<user_master>(
                    @"
            SELECT *
            FROM mastertenant.user_master
            WHERE tenant_code = @tenant_code
              AND deleted = false
            ORDER BY user_code;",
                    new { tenant_code }))
                    .ToList();

                var branches = await db.QueryAsync<UserBranchMaster>(
                    @"
            SELECT *
            FROM mastertenant.user_branch_master
            WHERE tenant_code = @tenant_code
              AND deleted = false;",
                    new { tenant_code });

                var departments = await db.QueryAsync<UserGroupMaster>(
                    @"
            SELECT *
            FROM user_group_master
            WHERE tenant_code = @tenant_code
              AND deleted = false;",
                    new { tenant_code });

                var branchLookup = branches.ToLookup(b => b.user_code);
                var deptLookup = departments.ToLookup(d => d.user_code);

                var result = users.Select(u => new GetUser
                {
                    user = u,
                    branch = branchLookup[u.user_code].ToList(),
                    department = deptLookup[u.user_code].ToList()
                }).ToList();

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"GetAllUsers failed: {ex.Message}", ex);
            }
        }

        public async Task<IList<product_role_templates>> GetProductRoles(string tenant_code)
        {
            try
            {
                using IDbConnection db = Connection();

                string sql = @"
                    SELECT *
                    FROM mastertenant.product_role_templates
                    WHERE tenant_code = @tenant_code;";

                var res = await db.QueryAsync<product_role_templates>(sql, new { tenant_code });

                return res.ToList();
            }
            catch (Exception ex)
            {
                return new List<product_role_templates>();
            }
        }

        // ─── Verify User ──────────────────────────────────────────────────────

        public async Task<string> VerifyUser(int user_code, string tenant_code)
        {
            try
            {
                using var db = Connection();

                var user = await db.QueryFirstOrDefaultAsync<user_master>(@"
                    SELECT * FROM mastertenant.user_master
                    WHERE user_code   = @user_code
                      AND tenant_code = @tenant_code
                      AND deleted     = false",
                    new { user_code, tenant_code });

                if (user == null) return "User not found";

                user.is_verified = true;
                await db.UpdateAsync(user);  // ← Dapper.Contrib

                return "User Verified";
            }
            catch (Exception ex)
            {
                throw new Exception($"VerifyUser failed: {ex.Message}");
            }
        }
    }
}