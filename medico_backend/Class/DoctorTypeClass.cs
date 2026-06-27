using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class DoctorTypeMasterClass
    {
        private readonly string db_conn;

        public DoctorTypeMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT TCODE
        // ─────────────────────────────────────────
        public async Task<int> GetNextTCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(tcode), 0) + 1
                           FROM doctor_type_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(DoctorTypeMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.tcode = await GetNextTCode(tenant_code);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO doctor_type_master
                    (
                        tcode,
                        tenant_code,
                        orderno,
                        name,
                        shortname,
                        description,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @tcode,
                        @tenant_code,
                        @orderno,
                        @name,
                        @shortname,
                        @description,
                        @deleted,
                        @usercode,
                        @computercode,
                        @entereddate,
                        @ibsdate
                    )";

                await db.ExecuteAsync(sql, data);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(DoctorTypeMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE doctor_type_master
                    SET
                        orderno      = @orderno,
                        name         = @name,
                        shortname    = @shortname,
                        description  = @description,
                        deleted      = @deleted,
                        usercode     = @usercode,
                        computercode = @computercode,
                        ibsdate      = @ibsdate,
                        tenant_code  = @tenant_code
                    WHERE tcode = @tcode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, data);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(int tcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE doctor_type_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE tcode = @tcode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { tcode, tenant_code });
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<DoctorTypeMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM doctor_type_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY orderno";

            var result = await db.QueryAsync<DoctorTypeMasterModel>(sql, new { tenant_code });
            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY TCODE
        // ─────────────────────────────────────────
        public async Task<DoctorTypeMasterModel?> GetByTCode(int tcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM doctor_type_master
                WHERE deleted = false
                AND tcode = @tcode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<DoctorTypeMasterModel>(
                sql, new { tcode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<DoctorTypeMasterModel>> SearchByName(string name, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM doctor_type_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY orderno";

            var result = await db.QueryAsync<DoctorTypeMasterModel>(
                sql, new { name = $"%{name}%", tenant_code });
            return result.ToList();
        }
    }
}