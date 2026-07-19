// ─────────────────────────────────────────
// CLASS (Dapper data access)
// ─────────────────────────────────────────
using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class VitalsClass
    {
        private readonly string db_conn;

        public VitalsClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // INSERT — generates a common daily token_no (not doctor-wise), resets every day
        // ─────────────────────────────────────────
        public async Task<InsertVitalsResult> Insert(VitalsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var todayUtc = DateTime.UtcNow.Date;

                // common counter across all doctors/investigations for this tenant, reset daily
                string tokenSql = @"
                    SELECT COALESCE(MAX(token_no::int), 0)
                    FROM vitals_entry
                    WHERE tenant_code = @tenant_code
                    AND entered_date::date = @today
                    AND deleted = false";

                var lastToken = await db.ExecuteScalarAsync<int>(
                    tokenSql, new { tenant_code = data.tenant_code, today = todayUtc });

                data.token_no = (lastToken + 1).ToString("D3");
                data.entered_date = DateTime.UtcNow;
                data.created_at = DateTime.UtcNow;
                data.updated_at = DateTime.UtcNow;
                data.deleted = false;

                var newId = await db.InsertAsync(data);

                return newId > 0
                    ? new InsertVitalsResult { message = "Success", token_no = data.token_no }
                    : new InsertVitalsResult { message = "Failed", token_no = null };
            }
            catch (Exception ex)
            {
                return new InsertVitalsResult { message = ex.Message, token_no = null };
            }
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(VitalsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var existing = await db.QueryFirstOrDefaultAsync<VitalsModel>(
                    "SELECT * FROM vitals_entry WHERE id = @id AND tenant_code = @tenant_code AND deleted = false",
                    new { id = data.id, tenant_code = data.tenant_code });

                if (existing == null)
                    return "Record not found for this tenant";

                // token_no is not editable once generated
                data.token_no = existing.token_no;
                data.entered_date = existing.entered_date;
                data.created_at = existing.created_at;
                data.updated_at = DateTime.UtcNow;
                data.deleted = existing.deleted;

                var result = await db.UpdateAsync(data);
                return result ? "Success" : "Failed";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // UPDATE STATUS ONLY (quick status change, e.g. from token/lab/scan screens)
        // ─────────────────────────────────────────
        public async Task<string> UpdateStatus(int id, string tenant_code, string status, int usercode, int computercode)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE vitals_entry
                    SET status = @status,
                        usercode = @usercode,
                        computercode = @computercode,
                        updated_at = @updated_at
                    WHERE id = @id
                    AND tenant_code = @tenant_code
                    AND deleted = false";

                var rows = await db.ExecuteAsync(sql, new
                {
                    id,
                    tenant_code,
                    status,
                    usercode,
                    computercode,
                    updated_at = DateTime.UtcNow
                });

                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE (soft delete → deleted = true)
        // ─────────────────────────────────────────
        public async Task<string> Delete(int id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE vitals_entry
                    SET deleted = true,
                        updated_at = @updated_at
                    WHERE id = @id
                    AND tenant_code = @tenant_code";

                var rows = await db.ExecuteAsync(sql, new { id, tenant_code, updated_at = DateTime.UtcNow });
                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL (active, non-deleted) — joined with customer & doctor for display
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    v.id,
                    v.tenant_code,
                    v.token_no,
                    v.custid,
                    c.name AS patient_name,
                    v.dcode,
                    d.name AS doctor_name,
                    v.investigation,
                    v.status,
                    v.entered_date,
                    v.usercode,
                    v.computercode,
                    v.created_at,
                    v.updated_at
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custid = v.custid
                LEFT JOIN doctor_master d ON d.dcode = v.dcode
                WHERE v.tenant_code = @tenant_code
                AND v.deleted = false
                ORDER BY v.created_at DESC";

            var result = await db.QueryAsync(sql, new { tenant_code });
            return result;
        }

        // ─────────────────────────────────────────
        // GET BY ID
        // ─────────────────────────────────────────
        public async Task<VitalsModel?> GetById(int id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM vitals_entry
                WHERE id = @id
                AND tenant_code = @tenant_code
                AND deleted = false";

            return await db.QueryFirstOrDefaultAsync<VitalsModel>(sql, new { id, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET BY STATUS (e.g. token display screen filtering)
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetByStatus(string status, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    v.id,
                    v.tenant_code,
                    v.token_no,
                    v.custid,
                    c.name AS patient_name,
                    v.dcode,
                    d.name AS doctor_name,
                    v.investigation,
                    v.status,
                    v.entered_date,
                    v.usercode,
                    v.computercode,
                    v.created_at,
                    v.updated_at
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custid = v.custid
                LEFT JOIN doctor_master d ON d.dcode = v.dcode
                WHERE v.status = @status
                AND v.tenant_code = @tenant_code
                AND v.deleted = false
                ORDER BY v.entered_date ASC";

            var result = await db.QueryAsync(sql, new { status, tenant_code });
            return result;
        }
    }

    public class InsertVitalsResult
    {
        public string message { get; set; } = "";
        public string? token_no { get; set; }
    }
}