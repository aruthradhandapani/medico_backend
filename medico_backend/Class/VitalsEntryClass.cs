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
        private readonly OgQueueClass ogQueue;

        public VitalsClass(IConfiguration configuration, OgQueueClass _ogQueue)
        {
            db_conn = configuration.GetConnectionString("conn");
            ogQueue = _ogQueue;
        }

        // ─────────────────────────────────────────
        // INSERT — generates a common daily token_no (not doctor-wise), resets every day
        // Also pushes into OG queue immediately if investigation = 'doctor'
        // ─────────────────────────────────────────
        public async Task<InsertVitalsResult> Insert(VitalsModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);
                db.Open();
                using var transaction = db.BeginTransaction();

                var todayUtc = DateTime.UtcNow.Date;

                // Lock on a hash of tenant_code+date so only one Insert per tenant/day runs the token logic at a time
                long lockKey = (tenant_code: data.tenant_code, date: todayUtc).GetHashCode();
                await db.ExecuteAsync("SELECT pg_advisory_xact_lock(@lockKey)", new { lockKey }, transaction);

                // Guard against the same insert landing twice — a double-click, a
                // client retry, whatever the cause — by reusing an identical entry
                // if it was created moments ago instead of creating another one.
                string dupSql = @"
            SELECT vitalentryid, token_no
            FROM vitals_entry
            WHERE tenant_code = @tenant_code
            AND custcode = @custcode
            AND investigation = @investigation
            AND test_name IS NOT DISTINCT FROM @test_name
            AND dcode IS NOT DISTINCT FROM @dcode
            AND deleted = false
            AND created_at > (now() - interval '5 seconds')
            ORDER BY created_at DESC
            LIMIT 1";

                var dup = await db.QueryFirstOrDefaultAsync<VitalsModel>(
                    dupSql,
                    new
                    {
                        tenant_code = data.tenant_code,
                        custcode = data.custcode,
                        investigation = data.investigation,
                        test_name = data.test_name,
                        dcode = data.dcode
                    },
                    transaction);

                if (dup != null)
                {
                    transaction.Commit();
                    return new InsertVitalsResult { message = "Success", token_no = dup.token_no };
                }

                string tokenSql = @"
            SELECT COALESCE(MAX(token_no::int), 0)
            FROM vitals_entry
            WHERE tenant_code = @tenant_code
            AND entered_date::date = @today
            AND deleted = false";

                var lastToken = await db.ExecuteScalarAsync<int>(
                    tokenSql, new { tenant_code = data.tenant_code, today = todayUtc }, transaction);

                data.token_no = (lastToken + 1).ToString("D3");
                data.entered_date = DateTime.UtcNow;
                data.created_at = DateTime.UtcNow;
                data.updated_at = DateTime.UtcNow;
                data.deleted = false;

                var newId = await db.InsertAsync(data, transaction);

                transaction.Commit();

                if (newId > 0)
                {
                    if (string.Equals(data.investigation, "doctor", StringComparison.OrdinalIgnoreCase)
                        && data.dcode.HasValue
                        && !string.IsNullOrEmpty(data.custcode))
                    {
                        await ogQueue.AddToQueue(data.tenant_code!, data.custcode!, data.dcode.Value, data.token_no!, data.arrival_time, data.test_name, "direct");

                    }
                    return new InsertVitalsResult { message = "Success", token_no = data.token_no };
                }

                return new InsertVitalsResult { message = "Failed", token_no = null };
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
                    "SELECT * FROM vitals_entry WHERE vitalentryid = @vitalentryid AND tenant_code = @tenant_code AND deleted = false",
                    new { vitalentryid = data.vitalentryid, tenant_code = data.tenant_code });

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
        // When lab/scan status becomes 'report_received', pushes patient into OG queue
        // ─────────────────────────────────────────
        public async Task<string> UpdateStatus(int vitalentryid, string tenant_code, string status, int usercode, int computercode)
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
                    WHERE vitalentryid = @vitalentryid
                    AND tenant_code = @tenant_code
                    AND deleted = false";

                var rows = await db.ExecuteAsync(sql, new
                {
                    vitalentryid,
                    tenant_code,
                    status,
                    usercode,
                    computercode,
                    updated_at = DateTime.UtcNow
                });

                if (rows > 0 && string.Equals(status, "report_received", StringComparison.OrdinalIgnoreCase))
                {
                    var v = await GetById(vitalentryid, tenant_code);
                    if (v != null
                        && (string.Equals(v.investigation, "lab", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(v.investigation, "scan", StringComparison.OrdinalIgnoreCase))
                        && v.dcode.HasValue
                        && !string.IsNullOrEmpty(v.custcode))
                    {
                        await ogQueue.AddToQueue(tenant_code, v.custcode!, v.dcode.Value, v.token_no!, v.arrival_time, v.test_name, "test_completed");
                    }
                }

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
        public async Task<string> Delete(int vitalentryid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE vitals_entry
                    SET deleted = true,
                        updated_at = @updated_at
                    WHERE vitalentryid = @vitalentryid
                    AND tenant_code = @tenant_code";

                var rows = await db.ExecuteAsync(sql, new { vitalentryid, tenant_code, updated_at = DateTime.UtcNow });
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
                    v.vitalentryid,
                    v.tenant_code,
                    v.token_no,
                    v.custcode,
                    c.name AS patient_name,
                    v.dcode,
                    d.name AS doctor_name,
                    v.investigation,
                    v.test_name,
                    v.status,
                    v.entered_date,
                    v.arrival_time,
                    v.usercode,
                    v.computercode,
                    v.created_at,
                    v.updated_at
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custcode = v.custcode
                LEFT JOIN doctor_master d ON d.dcode = v.dcode AND d.tenant_code = v.tenant_code
                WHERE v.tenant_code = @tenant_code
                AND v.deleted = false
                ORDER BY v.created_at DESC";

            var result = await db.QueryAsync(sql, new { tenant_code });
            return result;
        }

        // ─────────────────────────────────────────
        // GET BY ID
        // ─────────────────────────────────────────
        public async Task<VitalsModel?> GetById(int vitalentryid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM vitals_entry
                WHERE vitalentryid = @vitalentryid
                AND tenant_code = @tenant_code
                AND deleted = false";

            return await db.QueryFirstOrDefaultAsync<VitalsModel>(sql, new { vitalentryid, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET BY STATUS (e.g. token display screen filtering)
        // ─────────────────────────────────────────
        public async Task<IEnumerable<dynamic>> GetByStatus(string status, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT
                    v.vitalentryid,
                    v.tenant_code,
                    v.token_no,
                    v.custcode,
                    c.name AS patient_name,
                    v.dcode,
                    d.name AS doctor_name,
                    v.investigation,
                    v.test_name,
                    v.status,
                    v.entered_date,
                    v.arrival_time,
                    v.usercode,
                    v.computercode,
                    v.created_at,
                    v.updated_at
                FROM vitals_entry v
                LEFT JOIN customer_master c ON c.custcode = v.custcode
                LEFT JOIN doctor_master d ON d.dcode = v.dcode AND d.tenant_code = v.tenant_code
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