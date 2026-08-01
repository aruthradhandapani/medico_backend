using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using medico_backend.Model;

namespace medico_backend.Class
{
    public class OpChargeSlabClass
    {
        private readonly string db_conn;

        public OpChargeSlabClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn")!;
        }

        // ─────────────────────────────────────────
        // ADD SLABS — bulk, list-wise (e.g. all age brackets for one doctor at once)
        // ─────────────────────────────────────────
        public async Task<string> AddSlabs(List<OpChargeSlabModel> slabs, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                foreach (var slab in slabs)
                {
                    slab.tenant_code = tenant_code;
                    slab.created_at = DateTime.UtcNow;
                    slab.updated_at = DateTime.UtcNow;
                    slab.deleted = false;
                }

                await db.InsertAsync(slabs);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET SLABS (for one doctor)
        // ─────────────────────────────────────────
        public async Task<IEnumerable<OpChargeSlabModel>> GetSlabs(string tenant_code, int dcode)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT * FROM doctor_op_charge_slab
                WHERE tenant_code = @tenant_code
                AND dcode = @dcode
                AND deleted = false
                ORDER BY min_age";

            return await db.QueryAsync<OpChargeSlabModel>(sql, new { tenant_code, dcode });
        }

        // ─────────────────────────────────────────
        // DELETE SLAB (soft delete)
        // ─────────────────────────────────────────
        public async Task<string> DeleteSlab(int slabid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE doctor_op_charge_slab
                    SET deleted = true, updated_at = @now
                    WHERE slabid = @slabid AND tenant_code = @tenant_code";

                var rows = await db.ExecuteAsync(sql, new { slabid, tenant_code, now = DateTime.UtcNow });
                return rows > 0 ? "Success" : "Not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        
        // ─────────────────────────────────────────
        // UPDATE SLABS — bulk, list-wise
        // Each item must include its own slabid. Ownership is verified per row
        // before any update happens — if even one row fails validation, the
        // whole batch is rejected (no partial update).
        // ─────────────────────────────────────────
        public async Task<string> UpdateSlabs(List<OpChargeSlabModel> slabs, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var ids = slabs.Select(s => s.slabid).ToList();

                var existingRows = (await db.QueryAsync<OpChargeSlabModel>(
                    "SELECT * FROM doctor_op_charge_slab WHERE slabid = ANY(@ids) AND deleted = false",
                    new { ids })).ToDictionary(s => s.slabid);

                foreach (var slab in slabs)
                {
                    if (!existingRows.TryGetValue(slab.slabid, out var existing))
                        return $"Slab {slab.slabid} not found";
                    if (existing.tenant_code != tenant_code)
                        return $"Access denied for slab {slab.slabid}. Belongs to a different tenant.";

                    slab.tenant_code = tenant_code;
                    slab.created_at = existing.created_at;
                    slab.updated_at = DateTime.UtcNow;
                    slab.deleted = false;
                }

                await db.UpdateAsync(slabs);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}