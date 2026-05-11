using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class UserMasterClass
    {
        private readonly string db_conn;

        public UserMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────
        // GET NEXT USERCODE
        // ─────────────────────────────────────────
        public async Task<int> GetNextUserCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(usercode),0) + 1
                           FROM user_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(
                sql,
                new { tenant_code });
        }

        // ─────────────────────────────────────────
        // REGISTER
        // ─────────────────────────────────────────
        public async Task<string> Register(UserMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                // CHECK USER EXISTS
                string checkSql = @"SELECT COUNT(*)
                                    FROM user_master
                                    WHERE shortname = @shortname
                                    AND tenant_code = @tenant_code
                                    AND deleted = false";

                int exists = await db.ExecuteScalarAsync<int>(
                    checkSql,
                    new
                    {
                        data.shortname,
                        data.tenant_code
                    });

                if (exists > 0)
                    return "User already exists";

                data.usercode = await GetNextUserCode(data.tenant_code!);

                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                await db.InsertAsync(data);

                return "Register Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // LOGIN
        // ─────────────────────────────────────────
        public async Task<object> Login(
            LoginModel data,
            string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM user_master
                           WHERE shortname = @shortname
                           AND ""Password"" = @Password
                           AND tenant_code = @tenant_code
                           AND deleted = false";

            var user = await db.QueryFirstOrDefaultAsync<UserMasterModel>(
                sql,
                new
                {
                    data.shortname,
                    data.Password,
                    tenant_code
                });

            if (user == null)
            {
                return new
                {
                    success = false,
                    message = "Invalid Username or Password"
                };
            }

            return new
            {
                success = true,
                message = "Login Success",
                data = user
            };
        }

        // ─────────────────────────────────────────
        // RESET PASSWORD
        // ─────────────────────────────────────────
        public async Task<string> ResetPassword(
            ResetPasswordModel data,
            string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string checkSql = @"SELECT *
                                    FROM user_master
                                    WHERE shortname = @shortname
                                    AND ""Password"" = @oldpassword
                                    AND tenant_code = @tenant_code
                                    AND deleted = false";

                var user = await db.QueryFirstOrDefaultAsync<UserMasterModel>(
                    checkSql,
                    new
                    {
                        data.shortname,
                        data.oldpassword,
                        tenant_code
                    });

                if (user == null)
                    return "Old password incorrect";

                string updateSql = @"UPDATE user_master
                                     SET ""Password"" = @newpassword,
                                         ibsdate = now()
                                     WHERE usercode = @usercode";

                await db.ExecuteAsync(
                    updateSql,
                    new
                    {
                        newpassword = data.newpassword,
                        user.usercode
                    });

                return "Password Reset Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL USERS
        // ─────────────────────────────────────────
        public async Task<List<UserMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM user_master
                           WHERE deleted = false
                           AND tenant_code = @tenant_code
                           ORDER BY usercode";

            var res = await db.QueryAsync<UserMasterModel>(
                sql,
                new { tenant_code });

            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY USERCODE
        // ─────────────────────────────────────────
        public async Task<UserMasterModel?> GetByUserCode(
            int usercode,
            string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM user_master
                           WHERE usercode = @usercode
                           AND tenant_code = @tenant_code
                           AND deleted = false";

            return await db.QueryFirstOrDefaultAsync<UserMasterModel>(
                sql,
                new
                {
                    usercode,
                    tenant_code
                });
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(
            int usercode,
            string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE user_master
                               SET deleted = true,
                                   ibsdate = now()
                               WHERE usercode = @usercode
                               AND tenant_code = @tenant_code";

                int rows = await db.ExecuteAsync(
                    sql,
                    new
                    {
                        usercode,
                        tenant_code
                    });

                return rows > 0
                    ? "Success"
                    : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}