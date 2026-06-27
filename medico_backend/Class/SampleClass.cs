using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class SampleMasterClass
    {
        private readonly string db_conn;

        public SampleMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT SCODE
        // ─────────────────────────────────────────
        public async Task<decimal> GetNextSCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(scode), 0) + 1
                           FROM sample_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<decimal>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(SampleMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.scode = await GetNextSCode(tenant_code);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO sample_master
                    (
                        scode,
                        tenant_code,
                        orderno,
                        shortname,
                        name,
                        description,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @scode,
                        @tenant_code,
                        @orderno,
                        @shortname,
                        @name,
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
        public async Task<string> Update(SampleMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE sample_master
                    SET
                        orderno = @orderno,
                        shortname = @shortname,
                        name = @name,
                        description = @description,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate
                    WHERE scode = @scode
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
        public async Task<string> Delete(decimal scode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE sample_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE scode = @scode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { scode, tenant_code });

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
        public async Task<List<SampleMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM sample_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY orderno";

            var result = await db.QueryAsync<SampleMasterModel>(sql, new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY SCODE
        // ─────────────────────────────────────────
        public async Task<SampleMasterModel?> GetBySCode(decimal scode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM sample_master
                WHERE deleted = false
                AND scode = @scode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<SampleMasterModel>(
                sql,
                new { scode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<SampleMasterModel>> SearchByName(string name, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM sample_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY orderno";

            var result = await db.QueryAsync<SampleMasterModel>(
                sql,
                new { name = $"%{name}%", tenant_code });

            return result.ToList();
        }
    }
}