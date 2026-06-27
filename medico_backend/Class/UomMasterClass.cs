using Dapper;
using medico_backend.Model;
using Medico_Backend.Model;
using Npgsql;
using System.Data;

namespace Medico_Backend.Class
{
    public class UomMasterClass
    {
        private readonly string db_conn;

        public UomMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT UCODE
        // ─────────────────────────────────────────
        public async Task<decimal> GetNextUCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(ucode), 0) + 1
                           FROM uom_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<decimal>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(UomMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ucode = await GetNextUCode(tenant_code);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO uom_master
                    (
                        ucode,
                        tenant_code,
                        orderno,
                        name,
                        shortname,
                        decimalplaces,
                        description,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate,
                        packsize
                    )
                    VALUES
                    (
                        @ucode,
                        @tenant_code,
                        @orderno,
                        @name,
                        @shortname,
                        @decimalplaces,
                        @description,
                        @deleted,
                        @usercode,
                        @computercode,
                        @entereddate,
                        @ibsdate,
                        @packsize
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
        public async Task<string> Update(UomMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE uom_master
                    SET
                        orderno = @orderno,
                        name = @name,
                        shortname = @shortname,
                        decimalplaces = @decimalplaces,
                        description = @description,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        ibsdate = @ibsdate,
                        packsize = @packsize
                    WHERE ucode = @ucode
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
        public async Task<string> Delete(decimal ucode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE uom_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE ucode = @ucode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { ucode, tenant_code });

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
        public async Task<List<UomMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM uom_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY orderno";

            var result = await db.QueryAsync<UomMasterModel>(sql, new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY UCODE
        // ─────────────────────────────────────────
        public async Task<UomMasterModel?> GetByUCode(decimal ucode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM uom_master
                WHERE deleted = false
                AND ucode = @ucode
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<UomMasterModel>(
                sql,
                new { ucode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<UomMasterModel>> SearchByName(string name, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM uom_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY orderno";

            var result = await db.QueryAsync<UomMasterModel>(
                sql,
                new { name = $"%{name}%", tenant_code });

            return result.ToList();
        }
    }
}