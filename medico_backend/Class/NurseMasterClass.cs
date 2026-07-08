using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class NurseMasterClass
    {
        private readonly string db_conn;

        public NurseMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(NurseMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                var id = await db.InsertAsync(data);
                data.ncode = id;

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
        public async Task<string> Update(NurseMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.ibsdate = DateTime.UtcNow;

                var res = await db.UpdateAsync(data);
                return res ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // SOFT DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(int ncode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE public.nurse_master
                               SET deleted  = true,
                                   ibsdate  = now()
                               WHERE ncode        = @ncode
                               AND tenant_code    = @tenant_code";

                await db.ExecuteAsync(sql, new { ncode, tenant_code });
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
        public async Task<List<NurseMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.nurse_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND tenant_code    = @tenant_code
                           ORDER BY nursename";

            var res = await db.QueryAsync<NurseMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY NCODE
        // ─────────────────────────────────────────
        public async Task<NurseMasterModel?> GetByNcode(int ncode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.nurse_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND ncode        = @ncode
                           AND tenant_code  = @tenant_code";

            var res = await db.QueryFirstOrDefaultAsync<NurseMasterModel>(
                sql, new { ncode, tenant_code });
            return res;
        }

        // ─────────────────────────────────────────
        // GET BY TYPE (e.g. Staff Nurse, Head Nurse)
        // ─────────────────────────────────────────
        public async Task<List<NurseMasterModel>> GetByType(string ntype, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.nurse_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND ntype          = @ntype
                           AND tenant_code    = @tenant_code
                           ORDER BY nursename";

            var res = await db.QueryAsync<NurseMasterModel>(sql, new { ntype, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // SEARCH (by name or contact)
        // ─────────────────────────────────────────
        public async Task<List<NurseMasterModel>> Search(string key, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.nurse_master
                           WHERE (deleted IS NULL OR deleted = false)
                           AND tenant_code    = @tenant_code
                           AND (nursename ILIKE @key OR ncontact::text ILIKE @key)";

            var res = await db.QueryAsync<NurseMasterModel>(sql, new { tenant_code, key = $"%{key}%" });
            return res.ToList();
        }
    }
}