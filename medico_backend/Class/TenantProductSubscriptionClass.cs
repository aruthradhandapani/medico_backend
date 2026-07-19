using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class TenantProductSubscriptionClass
    {
        private readonly string db_conn;

        public TenantProductSubscriptionClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(TenantProductSubscriptionModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.created_at = DateTime.UtcNow;
                data.updated_at = DateTime.UtcNow;

                var newId = await db.InsertAsync(data);
                return newId > 0 ? "Success" : "Failed";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(TenantProductSubscriptionModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var existing = await db.QueryFirstOrDefaultAsync<TenantProductSubscriptionModel>(
                    "SELECT * FROM mastertenant.tenant_product_subscription WHERE id = @id AND tenant_code = @tenant_code",
                    new { id = data.id, tenant_code = data.tenant_code });

                if (existing == null)
                    return "Record not found for this tenant";

                data.created_at = existing.created_at;
                data.updated_at = DateTime.UtcNow;

                var result = await db.UpdateAsync(data);
                return result ? "Success" : "Failed";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE (soft delete → status = cancelled)
        // ─────────────────────────────────────────
        public async Task<string> Delete(int id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE mastertenant.tenant_product_subscription
                    SET status = 'cancelled',
                        updated_at = now()
                    WHERE id = @id
                    AND tenant_code = @tenant_code";

                var rows = await db.ExecuteAsync(sql, new { id, tenant_code });
                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<TenantProductSubscriptionModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM mastertenant.tenant_product_subscription
                WHERE tenant_code = @tenant_code
                ORDER BY created_at DESC";

            var result = await db.QueryAsync<TenantProductSubscriptionModel>(sql, new { tenant_code });
            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY ID
        // ─────────────────────────────────────────
        public async Task<TenantProductSubscriptionModel?> GetById(int id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM mastertenant.tenant_product_subscription
                WHERE id = @id
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<TenantProductSubscriptionModel>(
                sql, new { id, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET BY PRODUCT
        // ─────────────────────────────────────────
        public async Task<List<TenantProductSubscriptionModel>> GetByProduct(string product_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM mastertenant.tenant_product_subscription
                WHERE product_id = @product_id
                AND tenant_code = @tenant_code
                ORDER BY created_at DESC";

            var result = await db.QueryAsync<TenantProductSubscriptionModel>(
                sql, new { product_id, tenant_code });
            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET ALL — ACROSS ALL TENANTS (admin panel view)
        // Joins tenants + product_list for readable names
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetAllAcrossTenants()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    s.id,
                    s.tenant_code,
                    s.product_id,
                    s.amount_paid,
                    s.currency,
                    s.payment_mode,
                    s.transaction_id,
                    s.invoice_number,
                    s.payment_status,
                    s.billing_cycle,
                    s.start_date,
                    s.end_date,
                    s.status,
                    s.max_users,
                    s.purchased_by,
                    s.remarks,
                    s.created_at,
                    s.updated_at
                FROM mastertenant.tenant_product_subscription s
                ORDER BY s.created_at DESC";

            var result = await db.QueryAsync(sql);
            return result;
        }
    }
}