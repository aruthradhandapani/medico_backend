using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class TestTypeMasterClass
    {
        private readonly string db_conn;

        public TestTypeMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(TestTypeMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO test_type_master
                    (
                        tenant_code,
                        shortname,
                        name,
                        deleted,
                        usercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @tenant_code,
                        @shortname,
                        @name,
                        @deleted,
                        @usercode,
                        @entereddate,
                        @ibsdate
                    )
                    RETURNING ttid";

                data.ttid = await db.ExecuteScalarAsync<long>(sql, data);

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
        public async Task<string> Update(TestTypeMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE test_type_master
                    SET
                        shortname = @shortname,
                        name = @name,
                        deleted = @deleted,
                        usercode = @usercode,
                        ibsdate = @ibsdate
                    WHERE ttid = @ttid
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
        public async Task<string> Delete(long ttid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE test_type_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE ttid = @ttid
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { ttid, tenant_code });

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
        public async Task<List<TestTypeMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM test_type_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                ORDER BY ttid";

            var result = await db.QueryAsync<TestTypeMasterModel>(sql, new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY TTID
        // ─────────────────────────────────────────
        public async Task<TestTypeMasterModel?> GetByTtid(long ttid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM test_type_master
                WHERE deleted = false
                AND ttid = @ttid
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<TestTypeMasterModel>(
                sql,
                new { ttid, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY NAME
        // ─────────────────────────────────────────
        public async Task<List<TestTypeMasterModel>> SearchByName(string name, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM test_type_master
                WHERE deleted = false
                AND tenant_code = @tenant_code
                AND LOWER(name) LIKE LOWER(@name)
                ORDER BY ttid";

            var result = await db.QueryAsync<TestTypeMasterModel>(
                sql,
                new { name = $"%{name}%", tenant_code });

            return result.ToList();
        }
    }
}