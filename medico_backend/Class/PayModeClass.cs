using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class PaymodeMasterClass
    {
        private readonly string db_conn;

        public PaymodeMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // pmcode is generated per-tenant: pg_advisory_xact_lock keyed on the
        // tenant_code hash prevents a race between two concurrent inserts for
        // the same tenant computing the same "next" pmcode (same pattern used
        // for bncode generation in billno_master / CreateBillNoConfig).
        // ─────────────────────────────────────────
        public async Task<string> Insert(PaymodeMasterModel data)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                if (string.IsNullOrWhiteSpace(data.name))
                {
                    tx.Rollback();
                    return "name is required";
                }
                if (string.IsNullOrWhiteSpace(data.tenant_code))
                {
                    tx.Rollback();
                    return "tenant_code is required";
                }

                await db.ExecuteAsync(
                    "SELECT pg_advisory_xact_lock(hashtext(@t))",
                    new { t = data.tenant_code }, tx);

                decimal nextCode = await db.ExecuteScalarAsync<decimal>(
                    @"SELECT COALESCE(MAX(pmcode), 0) + 1
                      FROM public.paymode_master
                      WHERE tenant_code = @tenant_code",
                    new { data.tenant_code }, tx);

                data.pmcode = nextCode;
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                await db.InsertAsync(data, tx); // [ExplicitKey] → pmcode included in INSERT

                tx.Commit();
                return "Success";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(PaymodeMasterModel data)
        {
            try
            {
                if (data.pmcode <= 0)
                    return "Valid pmcode is required";
                if (string.IsNullOrWhiteSpace(data.tenant_code))
                    return "tenant_code is required";

                using IDbConnection db = new NpgsqlConnection(db_conn);

                var existing = await db.QueryFirstOrDefaultAsync<PaymodeMasterModel>(
                    @"SELECT * FROM public.paymode_master
                      WHERE pmcode = @pmcode AND tenant_code = @tenant_code",
                    new { data.pmcode, data.tenant_code });

                if (existing == null) return "No data found";

                data.ibsdate = DateTime.UtcNow;
                data.entereddate = existing.entereddate; // preserve original entered date

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
        public async Task<string> Delete(decimal pmcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE public.paymode_master
                               SET deleted  = true,
                                   ibsdate  = now()
                               WHERE pmcode      = @pmcode
                               AND tenant_code   = @tenant_code";

                int rows = await db.ExecuteAsync(sql, new { pmcode, tenant_code });
                return rows > 0 ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // RESTORE (undo soft delete)
        // ─────────────────────────────────────────
        public async Task<string> Restore(decimal pmcode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"UPDATE public.paymode_master
                               SET deleted  = false,
                                   ibsdate  = now()
                               WHERE pmcode      = @pmcode
                               AND tenant_code   = @tenant_code";

                int rows = await db.ExecuteAsync(sql, new { pmcode, tenant_code });
                return rows > 0 ? "Success" : "No data found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<PaymodeMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.paymode_master
                           WHERE deleted = false
                           AND tenant_code = @tenant_code
                           ORDER BY orderno";

            var res = await db.QueryAsync<PaymodeMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY PMCODE
        // ─────────────────────────────────────────
        public async Task<PaymodeMasterModel?> GetByCode(decimal pmcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT *
                           FROM public.paymode_master
                           WHERE pmcode      = @pmcode
                           AND tenant_code   = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<PaymodeMasterModel>(
                sql, new { pmcode, tenant_code });
        }
    }
}