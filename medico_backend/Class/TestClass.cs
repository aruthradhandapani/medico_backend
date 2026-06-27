using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class TestMasterClass
    {
        private readonly string db_conn;

        public TestMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<(string result, long? tcode)> Insert(TestMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.entereddate = DateTimeOffset.UtcNow;
                data.ibsdate = DateTimeOffset.UtcNow;
                data.deleted = false;

                var insertedTcode = await db.InsertAsync(data);
                return ("Success", insertedTcode);
            }
            catch (Exception ex)
            {
                return (ex.Message, null);
            }
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(TestMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTimeOffset.UtcNow;

                var updated = await db.UpdateAsync(data);
                return updated ? "Success" : "Data Not Found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // SOFT DELETE
        // ─────────────────────────────────────────
        public async Task<string> SoftDelete(long tcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                int result = await db.ExecuteAsync(
                    @"UPDATE test_master
                         SET deleted = true,
                             ibsdate = now()
                       WHERE tcode = @tcode
                         AND tenant_code = @tenant_code",
                    new { tcode, tenant_code });

                return result == 0 ? "Data Not Found" : "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<TestMasterModel>> Get(string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var result = await db.QueryAsync<TestMasterModel>(
                    @"SELECT *
                FROM test_master
               WHERE deleted = false
                 AND tenant_code = @tenant_code
               ORDER BY orderno",
                    new { tenant_code });

                return result.ToList();
            }
            catch { return []; }
        }

        // ─────────────────────────────────────────
        // GET BY TCODE
        // ─────────────────────────────────────────
        public async Task<TestMasterModel?> GetByTcode(long tcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                return await db.QueryFirstOrDefaultAsync<TestMasterModel>(
                    @"SELECT *
                FROM test_master
               WHERE deleted = false
                 AND tcode = @tcode
                 AND tenant_code = @tenant_code",
                    new { tcode, tenant_code });
            }
            catch { return null; }
        }
    }
}