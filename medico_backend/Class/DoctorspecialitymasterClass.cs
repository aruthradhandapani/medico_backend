using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class DoctorSpecialtyMasterClass
    {
        private readonly string db_conn;

        public DoctorSpecialtyMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT SPCODE
        // ─────────────────────────────────────────
        public async Task<int> GetNextSpCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(spcode), 0) + 1
                           FROM doctor_specialty_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(DoctorSpecialtyMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.spcode = await GetNextSpCode(tenant_code);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO doctor_specialty_master
                    (
                        spcode,
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
                        @spcode,
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
        public async Task<string> Update(DoctorSpecialtyMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE doctor_specialty_master
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
                    WHERE spcode = @spcode
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
        public async Task<string> Delete(int spcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE doctor_specialty_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE spcode = @spcode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { spcode, tenant_code });
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
        public async Task<List<DoctorSpecialtyMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM doctor_specialty_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY orderno";

            var result = await db.QueryAsync<DoctorSpecialtyMasterModel>(sql, new { tenant_code });
            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY SPCODE
        // ─────────────────────────────────────────
        public async Task<DoctorSpecialtyMasterModel?> GetBySpCode(int spcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM doctor_specialty_master
                WHERE deleted = false
                AND spcode = @spcode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<DoctorSpecialtyMasterModel>(
                sql, new { spcode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<DoctorSpecialtyMasterModel>> SearchByName(string name, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM doctor_specialty_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY orderno";

            var result = await db.QueryAsync<DoctorSpecialtyMasterModel>(
                sql, new { name = $"%{name}%", tenant_code });
            return result.ToList();
        }
    }
}